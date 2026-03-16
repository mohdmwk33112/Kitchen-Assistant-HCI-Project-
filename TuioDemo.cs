/*
	TUIO C# Demo - part of the reacTIVision project
	Copyright (c) 2005-2016 Martin Kaltenbrunner <martin@tuio.org>

	This program is free software; you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation; either version 2 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program; if not, write to the Free Software
	Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using TUIO;

	public class TuioDemo : Form , TuioListener
	{
		private TuioClient client;
		private Dictionary<long,TuioObject> objectList;
		private Dictionary<long,TuioCursor> cursorList;
		private Dictionary<long,TuioBlob> blobList;

		public class IngredientState {
			public string Name;
			public bool IsSliced;
			public bool IsOnPizza;
			public float LastAngle;
			public Color DrawColor;
		}
		private Dictionary<long, IngredientState> activeIngredients = new Dictionary<long, IngredientState>();
		private bool isPlateActive = false;
		private float PROXIMITY_THRESHOLD = 0.10f; // stricter proximity in normalized coords

		public static int width, height;
		private int window_width =  640;
		private int window_height = 480;
		private int window_left = 0;
		private int window_top = 0;
		private int screen_width = Screen.PrimaryScreen.Bounds.Width;
		private int screen_height = Screen.PrimaryScreen.Bounds.Height;

		private bool fullscreen;
		private bool verbose;

		private enum AppState {
			SignIn,
			Landing
		}
		private AppState currentState = AppState.SignIn;
		
		private Image backgroundImage = null;

		Font font = new Font("Segoe UI", 12.0f);
		Font titleFont = new Font("Segoe UI", 16.0f, FontStyle.Bold);
		Font scanFont = new Font("Segoe UI", 36.0f, FontStyle.Bold);
		private string signedInUser = null;
		SolidBrush fntBrush = new SolidBrush(Color.LightGray);
		SolidBrush bgrBrush = new SolidBrush(Color.FromArgb(0,0,64));
		SolidBrush promptBrush = new SolidBrush(Color.White);
		SolidBrush curBrush = new SolidBrush(Color.FromArgb(150, 0, 255, 255));
		SolidBrush objBrush = new SolidBrush(Color.FromArgb(150, 255, 100, 100));
		SolidBrush blbBrush = new SolidBrush(Color.FromArgb(150, 100, 255, 100));
		Pen curPen = new Pen(new SolidBrush(Color.Cyan), 2);

		public TuioDemo(int port) {
		
			verbose = false;
			fullscreen = false;
			width = window_width;
			height = window_height;

			this.ClientSize = new System.Drawing.Size(width, height);
			this.Name = "TuioDemo";
			this.Text = "TuioDemo";
			
			this.Closing+=new CancelEventHandler(Form_Closing);
			this.KeyDown+=new KeyEventHandler(Form_KeyDown);

			this.SetStyle( ControlStyles.AllPaintingInWmPaint |
							ControlStyles.UserPaint |
							ControlStyles.DoubleBuffer, true);

			objectList = new Dictionary<long,TuioObject>(128);
			cursorList = new Dictionary<long,TuioCursor>(128);
			blobList   = new Dictionary<long,TuioBlob>(128);
			
			try {
				string bgPath = System.IO.Path.Combine(Application.StartupPath, "background.jpeg");
				if (System.IO.File.Exists(bgPath)) {
					backgroundImage = Image.FromFile(bgPath);
				}
			} catch (Exception ex) {
				Console.WriteLine("Could not load background.jpeg: " + ex.Message);
			}

			client = new TuioClient(port);
			client.addTuioListener(this);

			client.connect();
		}

		private void Form_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {

 			if ( e.KeyData == Keys.F1) {
	 			if (fullscreen == false) {

					width = screen_width;
					height = screen_height;

					window_left = this.Left;
					window_top = this.Top;

					this.FormBorderStyle = FormBorderStyle.None;
		 			this.Left = 0;
		 			this.Top = 0;
		 			this.Width = screen_width;
		 			this.Height = screen_height;

		 			fullscreen = true;
	 			} else {

					width = window_width;
					height = window_height;

		 			this.FormBorderStyle = FormBorderStyle.Sizable;
		 			this.Left = window_left;
		 			this.Top = window_top;
		 			this.Width = window_width;
		 			this.Height = window_height;

		 			fullscreen = false;
	 			}
 			} else if ( e.KeyData == Keys.Escape) {
				this.Close();

 			} else if ( e.KeyData == Keys.V ) {
 				verbose=!verbose;
 			}

 		}

		private void Form_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			client.removeTuioListener(this);

			client.disconnect();
			System.Environment.Exit(0);
		}

		public void addTuioObject(TuioObject o) {
			lock(objectList) {
				objectList.Add(o.SessionID,o);
				if (o.SymbolID == 1) {
					isPlateActive = !isPlateActive; // Toggle plate presence
					
					// Reset ingredients if plate is removed
					if (!isPlateActive) {
						foreach (var state in activeIngredients.Values) {
							state.IsOnPizza = false;
						}
					}
				} else if (o.SymbolID >= 2 && o.SymbolID <= 4) {
					if (activeIngredients.TryGetValue(o.SymbolID, out IngredientState existingState)) {
						if (existingState.IsOnPizza) {
							// If scanned again while already on the pizza, remove it from the pizza
							activeIngredients.Remove(o.SymbolID);
						} else {
							// Not on pizza, just update the reference angle
							existingState.LastAngle = o.Angle;
						}
					} else {
						IngredientState state = new IngredientState { IsSliced = false, IsOnPizza = false, LastAngle = o.Angle };
						if (o.SymbolID == 2) { state.Name = "Tomato"; state.DrawColor = Color.Tomato; }
						else if (o.SymbolID == 3) { state.Name = "Chicken"; state.DrawColor = Color.NavajoWhite; }
						else if (o.SymbolID == 4) { state.Name = "Pepper"; state.DrawColor = Color.LimeGreen; }
						activeIngredients[o.SymbolID] = state;
					}
				}
			}
			if (o.SymbolID == 0) {
				if (currentState == AppState.SignIn) {
					signedInUser = "Nader";
					currentState = AppState.Landing;
				} else if (currentState == AppState.Landing) {
					signedInUser = null;
					currentState = AppState.SignIn;
				}
			}
			if (verbose) Console.WriteLine("add obj "+o.SymbolID+" ("+o.SessionID+") "+o.X+" "+o.Y+" "+o.Angle);
		}

		public void updateTuioObject(TuioObject o) {
			lock(objectList) {
				if (activeIngredients.TryGetValue(o.SymbolID, out IngredientState state)) {
					if (!state.IsOnPizza) {
						// Rotation Logic
						float deltaAngle = o.Angle - state.LastAngle;
						if (deltaAngle > Math.PI) deltaAngle -= (float)(2 * Math.PI);
						if (deltaAngle < -Math.PI) deltaAngle += (float)(2 * Math.PI);

						if (deltaAngle > 0.3f) { // Rotated Right -> Slice
							state.IsSliced = true;
							state.LastAngle = o.Angle;
						} else if (deltaAngle < -0.3f) { // Rotated Left -> Whole
							state.IsSliced = false;
							state.LastAngle = o.Angle;
						}

						// Proximity Logic: must be sliced to place on pizza
						if (isPlateActive && state.IsSliced) {
							// Fixed plate position (bottom center, normalized)
							float plateNx = 0.5f;
							float plateNy = 0.8f; 
							
							float dx = o.X - plateNx;
							float dy = o.Y - plateNy;
							float dist = (float)Math.Sqrt(dx * dx + dy * dy);
							if (dist < PROXIMITY_THRESHOLD) {
								state.IsOnPizza = true;
							}
						}
					}
				}
			}
			if (verbose) Console.WriteLine("set obj "+o.SymbolID+" "+o.SessionID+" "+o.X+" "+o.Y+" "+o.Angle+" "+o.MotionSpeed+" "+o.RotationSpeed+" "+o.MotionAccel+" "+o.RotationAccel);
		}

		public void removeTuioObject(TuioObject o) {
			lock(objectList) {
				objectList.Remove(o.SessionID);
				if (o.SymbolID >= 2 && o.SymbolID <= 4) {
					if (activeIngredients.TryGetValue(o.SymbolID, out IngredientState state)) {
						if (!state.IsOnPizza) {
							activeIngredients.Remove(o.SymbolID);
						}
					}
				}
			}
			if (verbose) Console.WriteLine("del obj "+o.SymbolID+" ("+o.SessionID+")");
		}

		public void addTuioCursor(TuioCursor c) {
			lock(cursorList) {
				cursorList.Add(c.SessionID,c);
			}
			if (verbose) Console.WriteLine("add cur "+c.CursorID + " ("+c.SessionID+") "+c.X+" "+c.Y);
		}

		public void updateTuioCursor(TuioCursor c) {
			if (verbose) Console.WriteLine("set cur "+c.CursorID + " ("+c.SessionID+") "+c.X+" "+c.Y+" "+c.MotionSpeed+" "+c.MotionAccel);
		}

		public void removeTuioCursor(TuioCursor c) {
			lock(cursorList) {
				cursorList.Remove(c.SessionID);
			}
			if (verbose) Console.WriteLine("del cur "+c.CursorID + " ("+c.SessionID+")");
 		}

		public void addTuioBlob(TuioBlob b) {
			lock(blobList) {
				blobList.Add(b.SessionID,b);
			}
			if (verbose) Console.WriteLine("add blb "+b.BlobID + " ("+b.SessionID+") "+b.X+" "+b.Y+" "+b.Angle+" "+b.Width+" "+b.Height+" "+b.Area);
		}

		public void updateTuioBlob(TuioBlob b) {
		
			if (verbose) Console.WriteLine("set blb "+b.BlobID + " ("+b.SessionID+") "+b.X+" "+b.Y+" "+b.Angle+" "+b.Width+" "+b.Height+" "+b.Area+" "+b.MotionSpeed+" "+b.RotationSpeed+" "+b.MotionAccel+" "+b.RotationAccel);
		}

		public void removeTuioBlob(TuioBlob b) {
			lock(blobList) {
				blobList.Remove(b.SessionID);
			}
			if (verbose) Console.WriteLine("del blb "+b.BlobID + " ("+b.SessionID+")");
		}

		public void refresh(TuioTime frameTime) {
			Invalidate();
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			// Getting the graphics object
			Graphics g = pevent.Graphics;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

			int w = Math.Max(1, width);
			int h = Math.Max(1, height);

			if (currentState == AppState.SignIn) {
				using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(20, 20, 30), Color.FromArgb(50, 50, 80), 45f)) {
					g.FillRectangle(brush, new Rectangle(0, 0, w, h));
				}

				string title = "Kitchen Assistant";
				SizeF titleSize = g.MeasureString(title, scanFont);
				
				string prompt = "Place card on the surface to start";
				SizeF promptSize = g.MeasureString(prompt, titleFont);

				int boxWidth = Math.Max((int)titleSize.Width, (int)promptSize.Width) + 80;
				int boxHeight = (int)(titleSize.Height + promptSize.Height) + 60;
				int boxX = (w - boxWidth) / 2;
				int boxY = (h - boxHeight) / 2;

				using (SolidBrush boxBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0))) {
					g.FillRectangle(boxBrush, boxX, boxY, boxWidth, boxHeight);
				}
				using (Pen boxPen = new Pen(Color.FromArgb(150, 255, 255, 255), 2)) {
					g.DrawRectangle(boxPen, boxX, boxY, boxWidth, boxHeight);
				}

				g.DrawString(title, scanFont, promptBrush, new PointF((w - titleSize.Width) / 2, boxY + 20));
				g.DrawString(prompt, titleFont, fntBrush, new PointF((w - promptSize.Width) / 2, boxY + titleSize.Height + 30));
				return;
			}

			if (backgroundImage != null) {
				g.DrawImage(backgroundImage, new Rectangle(0, 0, w, h));
			} else {
				using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(15, 15, 20), Color.FromArgb(30, 30, 40), 90f)) {
					g.FillRectangle(brush, new Rectangle(0, 0, w, h));
				}
			}

			if (signedInUser != null) {
				using (SolidBrush topBarBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) {
					g.FillRectangle(topBarBrush, 0, 0, w, 60);
				}
				g.DrawString("Welcome, " + signedInUser + "!", titleFont, new SolidBrush(Color.FromArgb(100, 200, 255)), new PointF(20, 15));
				
				string logoutText = "Scan card to Sign Out";
				SizeF logoutSize = g.MeasureString(logoutText, font);
				g.DrawString(logoutText, font, new SolidBrush(Color.LightGray), new PointF(w - logoutSize.Width - 20, 20));
			}

			// draw the cursor path
			if (cursorList.Count > 0) {
 			 lock(cursorList) {
			 foreach (TuioCursor tcur in cursorList.Values) {
					List<TuioPoint> path = tcur.Path;
					TuioPoint current_point = path[0];

					for (int i = 0; i < path.Count; i++) {
						TuioPoint next_point = path[i];
						g.DrawLine(curPen, current_point.getScreenX(width), current_point.getScreenY(height), next_point.getScreenX(width), next_point.getScreenY(height));
						current_point = next_point;
					}
					g.FillEllipse(curBrush, current_point.getScreenX(width) - height / 100, current_point.getScreenY(height) - height / 100, height / 50, height / 50);
					g.DrawString(tcur.CursorID + "", font, fntBrush, new PointF(tcur.getScreenX(width) - 10, tcur.getScreenY(height) - 10));
				}
			}
		 }

			// draw the objects
			if (objectList.Count > 0 || isPlateActive) {
 				lock(objectList) {
					// Fixed plate coordinates for drawing
					int plateScreenX = w / 2;
					int plateScreenY = (int)(h * 0.8f);

					// 1. Draw Plate
					if (isPlateActive) {
						int size = height / 3; // large plate
						
						using (SolidBrush crustBrush = new SolidBrush(Color.BurlyWood)) {
							g.FillEllipse(crustBrush, plateScreenX - size / 2, plateScreenY - size / 2, size, size);
						}
						using (SolidBrush sauceBrush = new SolidBrush(Color.DarkRed)) {
							g.FillEllipse(sauceBrush, plateScreenX - size * 0.45f, plateScreenY - size * 0.45f, size * 0.9f, size * 0.9f);
						}
						using (SolidBrush cheeseBrush = new SolidBrush(Color.Gold)) {
							g.FillEllipse(cheeseBrush, plateScreenX - size * 0.4f, plateScreenY - size * 0.4f, size * 0.8f, size * 0.8f);
						}
						g.DrawString("Plate (Pizza)", font, fntBrush, new PointF(plateScreenX - 30, plateScreenY - size/2 - 20));
					}

					// 2. Draw generic objects (ignore plate and ingredients)
					foreach (TuioObject tobj in objectList.Values) {
						if (tobj.SymbolID >= 1 && tobj.SymbolID <= 4) continue; // Skip Plate & Ingredients

						int ox = tobj.getScreenX(width);
						int oy = tobj.getScreenY(height);
						int size = height / 10;

						g.TranslateTransform(ox, oy);
						g.RotateTransform((float)(tobj.Angle / Math.PI * 180.0f));
						g.TranslateTransform(-ox, -oy);

						g.FillRectangle(objBrush, new Rectangle(ox - size / 2, oy - size / 2, size, size));

						g.TranslateTransform(ox, oy);
						g.RotateTransform(-1 * (float)(tobj.Angle / Math.PI * 180.0f));
						g.TranslateTransform(-ox, -oy);

						g.DrawString(tobj.SymbolID + "", font, fntBrush, new PointF(ox - 10, oy - 10));
					}

					// 3. Draw Ingredients
					int stackOffset = 0;
					foreach (var kvp in activeIngredients) {
						long symbolID = kvp.Key;
						IngredientState state = kvp.Value;
						
						TuioObject tobj = null;
						foreach(TuioObject obj in objectList.Values) {
							if (obj.SymbolID == symbolID) { tobj = obj; break; }
						}

						if (!state.IsOnPizza && tobj == null) continue;

						int ox, oy;
						int size = height / 12;
						float angleToDraw = 0f;

						if (state.IsOnPizza && isPlateActive) {
							// Draw on pizza (slightly offset so they don't exactly overlap)
							ox = plateScreenX + (stackOffset * 15 - 20);
							oy = plateScreenY + ((stackOffset % 2) * 20 - 10);
							stackOffset++;
						} else {
							// Draw at marker
							ox = tobj.getScreenX(width);
							oy = tobj.getScreenY(height);
							angleToDraw = (float)(tobj.Angle / Math.PI * 180.0f);
						}

						g.TranslateTransform(ox, oy);
						g.RotateTransform(angleToDraw);
						g.TranslateTransform(-ox, -oy);

						using (SolidBrush ingBrush = new SolidBrush(state.DrawColor)) {
							if (!state.IsSliced) {
								// Draw Whole
								g.FillEllipse(ingBrush, ox - size / 2, oy - size / 2, size, size);
							} else {
								// Draw Sliced (3 smaller circles)
								int sSize = size / 2;
								g.FillEllipse(ingBrush, ox - sSize, oy - sSize, sSize, sSize);
								g.FillEllipse(ingBrush, ox, oy - sSize, sSize, sSize);
								g.FillEllipse(ingBrush, ox - sSize/2, oy, sSize, sSize);
							}
						}

						g.TranslateTransform(ox, oy);
						g.RotateTransform(-angleToDraw);
						g.TranslateTransform(-ox, -oy);

						string label = (state.IsSliced ? "Sliced " : "") + state.Name;
						g.DrawString(label, font, fntBrush, new PointF(ox - 20, oy - size/2 - 20));
					}
				}
			}

			// draw the blobs
			if (blobList.Count > 0) {
				lock(blobList) {
					foreach (TuioBlob tblb in blobList.Values) {
						int bx = tblb.getScreenX(width);
						int by = tblb.getScreenY(height);
						float bw = tblb.Width*width;
						float bh = tblb.Height*height;

						g.TranslateTransform(bx, by);
						g.RotateTransform((float)(tblb.Angle / Math.PI * 180.0f));
						g.TranslateTransform(-bx, -by);

						g.FillEllipse(blbBrush, bx - bw / 2, by - bh / 2, bw, bh);

						g.TranslateTransform(bx, by);
						g.RotateTransform(-1 * (float)(tblb.Angle / Math.PI * 180.0f));
						g.TranslateTransform(-bx, -by);
						
						g.DrawString(tblb.BlobID + "", font, fntBrush, new PointF(bx, by));
					}
				}
			}
		}

		public static void Main(String[] argv) {
	 		int port = 0;
			switch (argv.Length) {
				case 1:
					port = int.Parse(argv[0],null);
					if(port==0) goto default;
					break;
				case 0:
					port = 3333;
					break;
				default:
					Console.WriteLine("usage: mono TuioDemo [port]");
					System.Environment.Exit(0);
					break;
			}
			
			TuioDemo app = new TuioDemo(port);
			Application.Run(app);
		}
	}
