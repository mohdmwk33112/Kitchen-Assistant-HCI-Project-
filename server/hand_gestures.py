import mediapipe as mp
import cv2
import time
import os
import pickle
import threading
from threading import Lock
from dollarpy import Recognizer, Template, Point

BaseOptions = mp.tasks.BaseOptions
HandLandmarker = mp.tasks.vision.HandLandmarker
HandLandmarkerOptions = mp.tasks.vision.HandLandmarkerOptions
VisionRunningMode = mp.tasks.vision.RunningMode

live_points = []
finger_x = -1
finger_y = -1
points_lock = Lock()
running = True


def send_classified_data(conn, gesture_name, confidence, x, y):
    msg = f"gesture;{gesture_name};{confidence:.2f};{x};{y}"
    conn.sendall((msg + "\n").encode("utf-8"))
    print("Sent to C#:", msg)


def getPoints(videoURL, label, show_video=True):
    try:
        video_src = int(videoURL)
    except ValueError:
        video_src = videoURL

    cap = cv2.VideoCapture(video_src)

    options = HandLandmarkerOptions(
        base_options=BaseOptions(model_asset_path="hand_landmarker.task"),
        running_mode=VisionRunningMode.VIDEO,
        num_hands=1
    )

    points = []

    with HandLandmarker.create_from_options(options) as landmarker:
        fallback_timestamp = 0

        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                break

            timestamp_ms = int(cap.get(cv2.CAP_PROP_POS_MSEC))
            if timestamp_ms <= 0 or timestamp_ms <= fallback_timestamp:
                timestamp_ms = fallback_timestamp + 33
            fallback_timestamp = timestamp_ms

            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

            try:
                hand_result = landmarker.detect_for_video(mp_image, timestamp_ms)

                if hand_result.hand_landmarks:
                    for hand_landmarks in hand_result.hand_landmarks:
                        index_finger = hand_landmarks[8]
                        h, w, _ = frame.shape
                        cx, cy = int(index_finger.x * w), int(index_finger.y * h)
                        cv2.circle(frame, (cx, cy), 10, (0, 255, 0), cv2.FILLED)
                        points.append(Point(index_finger.x, index_finger.y, 1))
                        break
            except Exception:
                pass

            if show_video:
                cv2.imshow(label, frame)
                if cv2.waitKey(10) & 0xFF == ord("q"):
                    break

    cap.release()
    if show_video:
        cv2.destroyAllWindows()
        cv2.waitKey(100)

    return points


def live_test_capture(videoURL, label):
    global live_points, finger_x, finger_y, running

    try:
        video_src = int(videoURL)
    except ValueError:
        video_src = videoURL

    cap = cv2.VideoCapture(video_src)

    options = HandLandmarkerOptions(
        base_options=BaseOptions(model_asset_path="hand_landmarker.task"),
        running_mode=VisionRunningMode.VIDEO,
        num_hands=1
    )

    with HandLandmarker.create_from_options(options) as landmarker:
        fallback_timestamp = 0

        while cap.isOpened() and running:
            ret, frame = cap.read()
            if not ret:
                break

            frame = cv2.flip(frame, 1)

            timestamp_ms = int(cap.get(cv2.CAP_PROP_POS_MSEC))
            if timestamp_ms <= 0 or timestamp_ms <= fallback_timestamp:
                timestamp_ms = fallback_timestamp + 33
            fallback_timestamp = timestamp_ms

            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

            try:
                hand_result = landmarker.detect_for_video(mp_image, timestamp_ms)

                if hand_result.hand_landmarks:
                    for hand_landmarks in hand_result.hand_landmarks:
                        index_finger = hand_landmarks[8]

                        h, w, _ = frame.shape
                        cx, cy = int(index_finger.x * w), int(index_finger.y * h)
                        cv2.circle(frame, (cx, cy), 10, (0, 255, 0), cv2.FILLED)

                        with points_lock:
                            live_points.append(Point(index_finger.x, index_finger.y, 1))
                            finger_x = cx
                            finger_y = cy
                        break
            except Exception:
                pass

            cv2.putText(frame, "Press C to clear points", (10, 30),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
            cv2.putText(frame, "Press Q to quit", (10, 60),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)

            cv2.imshow(label, frame)
            key = cv2.waitKey(10) & 0xFF

            if key == ord("c"):
                with points_lock:
                    live_points.clear()
                    finger_x = -1
                    finger_y = -1
                print("Current gesture points cleared.")

            elif key == ord("q"):
                running = False
                break

    cap.release()
    cv2.destroyAllWindows()
    cv2.waitKey(100)


def load_templates():
    template_cache_file = "gesture_templates.pkl"

    if os.path.exists(template_cache_file):
        with open(template_cache_file, "rb") as f:
            return pickle.load(f)

    gestures = {
        "Swipe Left": "left_swipe_train",
        "Swipe Right": "right_swipe_train",
        "Circle": "circle_train",
        "L Shape": "l_shape_train"
    }

    templates = []
    for name, prefix in gestures.items():
        for i in range(1, 6):
            video_file = f"{prefix}.mp4" if i == 1 else f"{prefix}{i}.mp4"
            pts = getPoints(video_file, f"{name} Training {i}", show_video=False)
            if pts:
                templates.append(Template(name, pts))

    if templates:
        with open(template_cache_file, "wb") as f:
            pickle.dump(templates, f)

    return templates


def gesture_worker(conn, templates):
    """Runs in background thread — only does recognition, no GUI."""
    recognizer = Recognizer(templates)
    last_result = None

    while running:
        with points_lock:
            current_points = live_points.copy()
            current_x = finger_x
            current_y = finger_y

        if len(current_points) > 5:
            try:
                result = recognizer.recognize(current_points)
                if result:
                    gesture_name, confidence = result
                    if confidence >= 0.3:
                        current_key = f"{gesture_name};{confidence:.2f};{current_x};{current_y}"
                        if current_key != last_result:
                            print(f"Best match: {gesture_name} (Confidence: {confidence:.2f})")
                            print(f"Finger coordinates: ({current_x}, {current_y})")
                            send_classified_data(conn, gesture_name, confidence, current_x, current_y)
                            last_result = current_key
            except Exception as e:
                print("Recognition error:", e)

        time.sleep(0.2)


def start_gesture_detection(conn):
    """
    Must be called from the main thread.
    Spawns recognition in background, runs camera + GUI on main thread.
    """
    global running
    running = True

    templates = load_templates()
    if not templates:
        print("No templates available.")
        return

    # Recognition worker runs in background thread
    t = threading.Thread(target=gesture_worker, args=(conn, templates), daemon=True)
    t.start()

    # Camera capture + cv2.imshow stays on main thread
    live_test_capture(0, "Testing Gesture")