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
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using TUIO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
namespace TuioDemo
{ 
        public class TuioDemo : Form, TuioListener
    {
        string[] RecipeLines;
        string[] StepsLines;
        private bool actionTriggered1 = false;
        private bool actionTriggered2 = false;
        private bool rotationTriggered = false; // Flag to track rotation-based data sequence
        private int currentTUIO = -1;
        private int currentpage = 0;
        private bool ViewRecipe = false;
        private bool ViewSteps = false;
        private Client TCP;
        private bool menuVisible = false;
        private TuioClient client;
        private Dictionary<long, TuioObject> objectList;
        private Dictionary<long, TuioCursor> cursorList;
        private Dictionary<long, TuioBlob> blobList;
        public static int width, height;
        private int window_width = 640;
        private int window_height = 480;
        private int window_left = 0;
        private int window_top = 0;
        private int screen_width = Screen.PrimaryScreen.Bounds.Width;
        private int screen_height = Screen.PrimaryScreen.Bounds.Height;
        private int activeID = -1;
        private bool fullscreen;
        private bool verbose;
        private string Recipe;
        private string Steps;
        private int waitingForRecipeStep = 0; // 0: Idle, 1: Waiting for Recipe, 2: Waiting for Steps
        Font font = new Font("Arial", 10.0f);
        SolidBrush fntBrush = new SolidBrush(Color.White);
        SolidBrush bgrBrush = new SolidBrush(Color.FromArgb(0, 0, 64));
        SolidBrush curBrush = new SolidBrush(Color.FromArgb(192, 0, 192));
        SolidBrush objBrush = new SolidBrush(Color.FromArgb(64, 0, 0));
        SolidBrush blbBrush = new SolidBrush(Color.FromArgb(64, 64, 64));
        Pen curPen = new Pen(new SolidBrush(Color.Blue), 1);
        private PictureBox pictureBox2;
        private RichTextBox richTextBox1;
        private Button button4;
        private Button button6;
        private Button button5;
        private Button button3;
        private Button button2;
        private Button button1;
        private RichTextBox richTextBox3;
        private RichTextBox richTextBox4;
        private Point mouseCoords = new Point(0, 0);
        public TuioDemo(int port)
        {
            this.KeyPreview = true;
            verbose = false;
            fullscreen = false;
            width = window_width;
            height = window_height;
            this.MouseMove += (sender, e) => {
                mouseCoords.X = e.X;
                mouseCoords.Y = e.Y;
            };
            ClientSize = new Size(width, height);
            Name = "TuioDemo";
            Text = "TuioDemo";
            InitializeComponent();
            Task.Run(() => MessageListener());
            Closing += new CancelEventHandler(Form_Closing);
            KeyDown += new KeyEventHandler(Form_KeyDown);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                            ControlStyles.UserPaint |
                            ControlStyles.DoubleBuffer, true);

            objectList = new Dictionary<long, TuioObject>(128);
            cursorList = new Dictionary<long, TuioCursor>(128);
            blobList = new Dictionary<long, TuioBlob>(128);
            TCP = new Client();
            TCP.connectToSocket("26.148.131.172", 65434);//server

            client = new TuioClient(port);
            client.addTuioListener(this);
            Console.WriteLine("DEBUG: TUIO Client connecting to port " + port);
            client.connect();
        }
        private void ShowCircularMenu(Point center)
        {
            double angleStep = 2 * Math.PI / 6; // Divide the circle into 6 parts
            int menuRadius = 100; // Distance from center to buttons

            Button[] buttons = { button1, button2, button3, button4, button5, button6 };

            for (int i = 0; i < buttons.Length; i++)
            {
                // 1. Calculate position
                double angle = i * angleStep;
                int x = center.X + (int)(menuRadius * Math.Cos(angle)) - (buttons[i].Width / 2);
                int y = center.Y + (int)(menuRadius * Math.Sin(angle)) - (buttons[i].Height / 2);

                // 2. Move to PictureBox2 layer (the GIF)
                buttons[i].Location = new Point(x, y);
                buttons[i].Visible = true;
                buttons[i].Enabled = true;
                buttons[i].BringToFront();

                // 3. Make them look "Stylized"
                buttons[i].FlatStyle = FlatStyle.Flat;
                buttons[i].FlatAppearance.BorderSize = 2;
                buttons[i].BackColor = Color.FromArgb(60, 180, 220); // Teal
                buttons[i].ForeColor = Color.White;
                buttons[i].Font = new Font("Segoe UI", 8, FontStyle.Bold);
                
                // Make them truly circular
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(0, 0, buttons[i].Width, buttons[i].Height);
                buttons[i].Region = new Region(path);
            }
            menuVisible = true;
        }
        private void Form_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.F1)
            {
                if (fullscreen == false)
                {

                    width = screen_width;
                    height = screen_height;

                    window_left = Left;
                    window_top = Top;

                    FormBorderStyle = FormBorderStyle.None;
                    Left = 0;
                    Top = 0;
                    Width = screen_width;
                    Height = screen_height;

                    fullscreen = true;
                }
                else
                {

                    width = window_width;
                    height = window_height;

                    FormBorderStyle = FormBorderStyle.Sizable;
                    Left = window_left;
                    Top = window_top;
                    Width = window_width;
                    Height = window_height;

                    fullscreen = false;
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();

            }
            else if (e.KeyCode == Keys.V)
            {
                verbose = !verbose;
            }
            if (e.KeyCode == Keys.C) // LOGIC TO TEST CIRCULAR MENU
            {
                if (!menuVisible)
                {
                    ShowCircularMenu(mouseCoords);
                }
                else
                {
                    Button[] buttons = { button1, button2, button3, button4, button5, button6 };
                    foreach (var btn in buttons) btn.Visible = false;
                    menuVisible = false;
                }
            }

        }

        private void Form_Closing(object sender, CancelEventArgs e)
        {
            client.removeTuioListener(this);
            client.disconnect();
            Environment.Exit(0);
        }

        public void addTuioObject(TuioObject o)
        {
            Console.WriteLine("DEBUG: TUIO Object Added! ID=" + o.SymbolID);
            lock (objectList)
            {
                objectList.Add(o.SessionID, o);
            }
            
            // If logged in, send the ID directly from the event
            if (activeID == 1)
            {
                // Only reset if it's a truly new scan (different ID)
                if (o.SymbolID != currentTUIO)
                {
                    string idMessage = "recipe_id" + ";" + o.SymbolID.ToString() + "\n";
                    TCP.sendMessage(idMessage);
                    Console.WriteLine("TUIO Scan Sent (Event): " + idMessage);
                    
                    // CRITICAL: Update state BEFORE Invoke to prevent race conditions
                    currentTUIO = (int)o.SymbolID;
                    if (waitingForRecipeStep == 0) { waitingForRecipeStep = 1; }
                    
                    
                    currentpage = 0; // Reset pagination
                    rotationTriggered = false; // Ensure scan shows ingredients first
                    
                    this.Invoke((MethodInvoker)delegate {
                        richTextBox1.Text = "Loading recipe for ID " + o.SymbolID + "...";
                        richTextBox1.Visible = true;
                        // Only reset these if a new object is scanned, not on every add event for the same object
                        RecipeLines = null;
                        StepsLines = null;
                        actionTriggered1 = false;
                        actionTriggered2 = false;
                        this.Invalidate();
                    });
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate { this.Invalidate(); });
            }
            if (verbose) Console.WriteLine("add obj " + o.SymbolID + " (" + o.SessionID + ") " + o.X + " " + o.Y + " " + o.Angle);
        }

        public void updateTuioObject(TuioObject o)
        {
            if (activeID == 1 && o.SymbolID == currentTUIO)
            {
                float degrees = (float)(o.Angle * 180.0f / Math.PI);
                // Rotate to the right (between 180 and 270 degrees)
                if (degrees <= 270 && degrees > 180)
                {
                    if (!actionTriggered2)
                    {
                        string idMessage = "confirm" +"\n";
                        TCP.sendMessage(idMessage);
                        Console.WriteLine("TUIO Rotation (Steps Requested): " + idMessage);
                        
                        // CRITICAL: Update state BEFORE Invoke to prevent race conditions
                        rotationTriggered = true; // Signal that we expect the full sequence after rotation
                        waitingForRecipeStep = 2; // WE ARE WAITING FOR STEPS (State 2) NOT RECIPE (State 1)

                        this.Invoke((MethodInvoker)delegate {
                            richTextBox1.Text = "Loading steps for ID " + o.SymbolID + "...";
                            richTextBox1.Visible = true;
                        });
                        
                        actionTriggered2 = true;
                    }
                }
            }

            if (verbose) Console.WriteLine("set obj " + o.SymbolID + " " + o.SessionID + " " + o.X + " " + o.Y + " " + o.Angle + " " + o.MotionSpeed + " " + o.RotationSpeed + " " + o.MotionAccel + " " + o.RotationAccel);
        }

        public void removeTuioObject(TuioObject o)
        {
            Console.WriteLine("DEBUG: TUIO Object Removed! ID=" + o.SymbolID);
            lock (objectList)
            {
                objectList.Remove(o.SessionID);
            }
            
            // Reset currentTUIO if the active object is removed
            if (o.SymbolID == currentTUIO)
            {
                currentTUIO = -1; 
                Console.WriteLine("Object removed. Ready for next scan.");
            }
            this.Invoke((MethodInvoker)delegate { this.Invalidate(); });
            if (verbose) Console.WriteLine("del obj " + o.SymbolID + " (" + o.SessionID + ")");
        }

        public void addTuioCursor(TuioCursor c)
        {
            lock (cursorList)
            {
                cursorList.Add(c.SessionID, c);
            }
            if (verbose) Console.WriteLine("add cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y);
        }

        public void updateTuioCursor(TuioCursor c)
        {
            if (verbose) Console.WriteLine("set cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y + " " + c.MotionSpeed + " " + c.MotionAccel);
        }

        public void removeTuioCursor(TuioCursor c)
        {
            lock (cursorList)
            {
                cursorList.Remove(c.SessionID);
            }
            if (verbose) Console.WriteLine("del cur " + c.CursorID + " (" + c.SessionID + ")");
        }

        public void addTuioBlob(TuioBlob b)
        {
            lock (blobList)
            {
                blobList.Add(b.SessionID, b);
            }
            if (verbose) Console.WriteLine("add blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area);
        }

        public void updateTuioBlob(TuioBlob b)
        {

            if (verbose) Console.WriteLine("set blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area + " " + b.MotionSpeed + " " + b.RotationSpeed + " " + b.MotionAccel + " " + b.RotationAccel);
        }

        public void removeTuioBlob(TuioBlob b)
        {
            lock (blobList)
            {
                blobList.Remove(b.SessionID);
            }
            if (verbose) Console.WriteLine("del blb " + b.BlobID + " (" + b.SessionID + ")");
        }

        public void refresh(TuioTime frameTime)
        {
            Invalidate();
        }
        public void ViewRecipes(string[] RecipeLines)
        {
            Console.WriteLine("DEBUG: ViewRecipes calling UI update...");
            if (RecipeLines == null || RecipeLines.Length < 2) return;
            
            richTextBox4.Clear();
            
            // Header: Recipe Name
            richTextBox4.SelectionFont = new Font("Segoe UI", 16, FontStyle.Bold);
            richTextBox4.SelectionColor = Color.FromArgb(20, 30, 50); // Navy
            richTextBox4.AppendText("RECIPE: " + RecipeLines[0].Trim().ToUpper() + "\n\n");
            
            // Description
            richTextBox4.SelectionFont = new Font("Segoe UI", 11, FontStyle.Italic);
            richTextBox4.SelectionColor = Color.DimGray;
            richTextBox4.AppendText(RecipeLines[1].Trim() + "\n\n");
            
            // Ingredients Header
            richTextBox4.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
            richTextBox4.SelectionColor = Color.DarkSlateGray;
            richTextBox4.AppendText("INGREDIENTS:\n");
            
            // Ingredient List
            richTextBox4.SelectionFont = new Font("Segoe UI", 11, FontStyle.Regular);
            richTextBox4.SelectionColor = Color.Black;
            for (int i = 2; i < RecipeLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(RecipeLines[i]))
                {
                    richTextBox4.AppendText("  • " + RecipeLines[i].Trim() + "\n");
                }
            }
            
            // richTextBox3.Visible = false; // Removed
            richTextBox4.Visible = true;
            richTextBox4.BringToFront();
            richTextBox1.Visible = true; 
            ViewSteps = false; 
            ViewRecipe = true;
        }
        public void ViewStepsList(string[] StepLines)
        {
            Console.WriteLine("DEBUG: ViewStepsList called with " + (StepLines?.Length ?? 0) + " elements");
            if (StepLines == null || StepLines.Length == 0) return;
            richTextBox3.Clear();
            
            // Clean up empty entries (like trailing semicolons)
            var validLines = StepLines.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            // Check if the lines seem to be in [Num];[Text] format or just [Text]
            bool isInterleaved = validLines.Length >= 2 && validLines[0].Length < 4 && int.TryParse(validLines[0], out _);

            if (isInterleaved)
            {
                for (int i = 0; i < validLines.Length - 1; i+=2)
                {
                    string stepNum = validLines[i].Trim();
                    string stepText = validLines[i+1].Trim();
                    
                    // Only display based on current page (5 steps = 10 interleaved elements per page)
                    int stepIndex = i / 2;
                    if (stepIndex >= currentpage && stepIndex < currentpage + 5)
                    {
                        richTextBox3.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
                        richTextBox3.SelectionColor = Color.FromArgb(60, 180, 220); // Teal
                        richTextBox3.AppendText("STEP " + stepNum + ": ");
                        
                        richTextBox3.SelectionFont = new Font("Segoe UI", 12, FontStyle.Regular);
                        richTextBox3.SelectionColor = Color.Black;
                        richTextBox3.AppendText(stepText + "\n\n");
                    }
                }
            }
            else
            {
                // Simple list format
                for (int i = currentpage; i < Math.Min(currentpage + 5, validLines.Length); i++)
                {
                    richTextBox3.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
                    richTextBox3.SelectionColor = Color.FromArgb(60, 180, 220); // Teal
                    richTextBox3.AppendText("STEP " + (i + 1) + ": ");
                    
                    richTextBox3.SelectionFont = new Font("Segoe UI", 12, FontStyle.Regular);
                    richTextBox3.SelectionColor = Color.Black;
                    richTextBox3.AppendText(validLines[i].Trim() + "\n\n");
                }
            }
            richTextBox3.Visible = true;
            richTextBox3.BringToFront();
            richTextBox4.Visible = true; // Keep ingredients visible
            ViewSteps = true; 
            ViewRecipe = false;
        }
        async void MessageListener()
        {
            while (true)
            {
                string message = TCP.recieveMessage();
                if (message != null)
                {
                    Console.WriteLine("DEBUG: Received Message '" + message + "' (activeID=" + activeID + ")");
                    if (activeID == -1)
                    {
                        // Handle Login
                        this.Invoke((MethodInvoker)delegate {
                            activeID = 1;
                            richTextBox1.Text = "Scan a recipe to start!";
                        });
                    }
                    else if (waitingForRecipeStep == 1)
                    {
                        // Handle Recipe Data (Name, Description, Ingredients)
                        Console.WriteLine("DEBUG: State 1 - Recipe received!");
                        Recipe = message;
                        waitingForRecipeStep = 2; // Expect steps next
                        this.Invoke((MethodInvoker)delegate {
                            RecipeLines = Recipe.Split(';');
                            Console.WriteLine("DEBUG: State 1 processing. rotationTriggered=" + rotationTriggered);
                            // Only show ingredients if this was NOT a rotation request
                            if (!rotationTriggered) ViewRecipes(RecipeLines);
                        });
                    }
                    else if (waitingForRecipeStep == 2)
                    {
                        // Handle Steps Data
                        Console.WriteLine("DEBUG: State 2 - Steps received!");
                        Steps = message;
                        waitingForRecipeStep = 0;
                        this.Invoke((MethodInvoker)delegate {
                            StepsLines = Steps.Split(';');
                            Console.WriteLine("DEBUG: State 2 processing. rotationTriggered=" + rotationTriggered);
                            // If this was a rotation request, show the steps now
                            if (rotationTriggered)
                            {
                                ViewStepsList(StepsLines);
                                rotationTriggered = false; // Reset flag after use
                            }
                        });
                    }
                    else
                    {
                        // Handle Gestures
                        if (message == "circle")
                        {
                            this.Invoke((MethodInvoker)delegate {
                                ShowCircularMenu(mouseCoords);
                            });
                        }
                        else if (message == "swipel")
                        {
                            if (StepsLines != null && currentpage + 5 < StepsLines.Length)
                            {
                                currentpage += 5;
                                this.Invoke((MethodInvoker)delegate { ViewStepsList(StepsLines); });
                            }
                        }
                        else if (message == "swiper")
                        {
                            if (currentpage >= 5)
                            {
                                currentpage -= 5;
                                this.Invoke((MethodInvoker)delegate { ViewStepsList(StepsLines); });
                            }
                        }
                    }
                }
                await Task.Delay(100);
            }
        }
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Getting the graphics object
            Graphics g = pevent.Graphics;
            g.FillRectangle(bgrBrush, new Rectangle(0, 0, width, height));
            pictureBox2.SendToBack();
            
            if (activeID == -1)
            {
                return;
            }
            
            // Log once in a while or when state changes
            Console.WriteLine("DEBUG: activeID=" + activeID + " ObjectCount=" + objectList.Count);
            if (activeID == 1)//LOGIN CONFIRMED SO WE CAN WORK HERE
            {
                lock (objectList)
                {
                    foreach (TuioObject tobj in objectList.Values)
                    {

                        float degrees = (float)(tobj.Angle * 180.0f / Math.PI);
                        if (tobj.SymbolID == currentTUIO)
                        {
                            if (degrees >= 90 && degrees < 180)
                            {
                                if (!actionTriggered1)
                                {
                                    TCP.sendMessage("fav");
                                    actionTriggered1 = true;
                                }
                            }
                            else if (degrees <= 270 && degrees > 180)
                            {
                                if (!actionTriggered2)
                                {
                                    ViewStepsList(StepsLines);
                                    actionTriggered2 = true;
                                }
                            }
                        }
                    }
                    
                }
            }
            // draw the cursor path //WE WILL NEED THE CURSOR PART HERE TO UPDATE THE CIRCULAR MENU LOCATION
            if (cursorList.Count > 0)
            {
                lock (cursorList)
                {
                    foreach (TuioCursor tcur in cursorList.Values)
                    {
                        List<TuioPoint> path = tcur.Path;
                        TuioPoint current_point = path[0];
                        for (int i = 0; i < path.Count; i++)
                        {
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
            if (objectList.Count > 0)//dont think this is useful rn
            {
                lock (objectList)
                {
                    foreach (TuioObject tobj in objectList.Values)
                    {
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
                }
            }

            // draw the blobs
            if (blobList.Count > 0)
            {
                lock (blobList)
                {
                    foreach (TuioBlob tblb in blobList.Values)
                    {
                        int bx = tblb.getScreenX(width);
                        int by = tblb.getScreenY(height);
                        float bw = tblb.Width * width;
                        float bh = tblb.Height * height;

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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TuioDemo));
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.button4 = new System.Windows.Forms.Button();
            this.button6 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.richTextBox3 = new System.Windows.Forms.RichTextBox();
            this.richTextBox4 = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox2.Enabled = false;
            this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
            this.pictureBox2.Location = new System.Drawing.Point(0, 0);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(1264, 681);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox2.TabIndex = 1;
            this.pictureBox2.TabStop = false;
            // 
            // richTextBox1
            // 
            this.richTextBox1.BackColor = System.Drawing.Color.FromArgb(20, 30, 50); // Navy Blue
            this.richTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBox1.Enabled = false;
            this.richTextBox1.ForeColor = System.Drawing.Color.White;
            this.richTextBox1.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBox1.Location = new System.Drawing.Point(50, 20);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ReadOnly = true;
            this.richTextBox1.Size = new System.Drawing.Size(1130, 60);
            this.richTextBox1.TabIndex = 6;
            this.richTextBox1.TabStop = false;
            this.richTextBox1.SelectionAlignment = HorizontalAlignment.Center;
            this.richTextBox1.Text = "Status: Kitchen Assistant Online";
            // 
            // button4
            // 
            this.button4.BackColor = System.Drawing.Color.FromArgb(60, 180, 220); // Teal
            this.button4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button4.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button4.ForeColor = System.Drawing.Color.White;
            this.button4.Location = new System.Drawing.Point(229, 12);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(65, 65);
            this.button4.TabIndex = 4;
            this.button4.Text = "LOGIN";
            this.button4.UseVisualStyleBackColor = false;
            this.button4.Visible = false;
            // 
            // button6
            // 
            this.button6.BackColor = System.Drawing.Color.FromArgb(60, 180, 220); // Teal
            this.button6.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button6.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button6.ForeColor = System.Drawing.Color.White;
            this.button6.Location = new System.Drawing.Point(76, 12);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(65, 65);
            this.button6.TabIndex = 6;
            this.button6.Text = "NEXT";
            this.button6.UseVisualStyleBackColor = false;
            this.button6.Visible = false;
            // 
            // button5
            // 
            this.button5.BackColor = System.Drawing.Color.Crimson; // Exit red
            this.button5.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button5.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button5.ForeColor = System.Drawing.Color.White;
            this.button5.Location = new System.Drawing.Point(127, 12);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(65, 65);
            this.button5.TabIndex = 5;
            this.button5.Text = "EXIT";
            this.button5.UseVisualStyleBackColor = false;
            this.button5.Visible = false;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.FromArgb(60, 180, 220); // Teal
            this.button3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button3.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button3.ForeColor = System.Drawing.Color.White;
            this.button3.Location = new System.Drawing.Point(178, 12);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(65, 65);
            this.button3.TabIndex = 3;
            this.button3.Text = "CLEAR";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Visible = false;
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.FromArgb(60, 180, 220); // Teal
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button2.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button2.ForeColor = System.Drawing.Color.White;
            this.button2.Location = new System.Drawing.Point(280, 12);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(65, 65);
            this.button2.TabIndex = 2;
            this.button2.Text = "STEPS";
            this.button2.UseVisualStyleBackColor = false;
            this.button2.Visible = false;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(60, 180, 220); // Teal
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(25, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(65, 65);
            this.button1.TabIndex = 1;
            this.button1.Text = "RECIPE";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Visible = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // richTextBox3
            // 
            this.richTextBox3.BackColor = System.Drawing.Color.WhiteSmoke;
            this.richTextBox3.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.richTextBox3.Location = new System.Drawing.Point(630, 100);
            this.richTextBox3.Name = "richTextBox3";
            this.richTextBox3.Size = new System.Drawing.Size(550, 500);
            this.richTextBox3.TabIndex = 8;
            this.richTextBox3.Text = "";
            this.richTextBox3.Visible = true;
            // 
            // richTextBox4
            // 
            this.richTextBox4.BackColor = System.Drawing.Color.WhiteSmoke;
            this.richTextBox4.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.richTextBox4.Location = new System.Drawing.Point(50, 100);
            this.richTextBox4.Name = "richTextBox4";
            this.richTextBox4.Size = new System.Drawing.Size(550, 500);
            this.richTextBox4.TabIndex = 10;
            this.richTextBox4.Text = "";
            this.richTextBox4.Visible = true;
            // 
            // TuioDemo
            // 
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1264, 681);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.richTextBox4);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.button6);
            this.Controls.Add(this.richTextBox3);
            this.Controls.Add(this.pictureBox2);
            this.Name = "TuioDemo";
            this.TransparencyKey = System.Drawing.Color.Transparent;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);

        }

        private void TuioDemo_KeyDown(object sender, KeyEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }
        class Client
        {
            public NetworkStream stream;
            public TcpClient client;

            public bool connectToSocket(string host, int portNumber)
            {
                try
                {
                    client = new TcpClient(host, portNumber);
                    stream = client.GetStream();
                    Console.WriteLine("connection made ! with " + host);
                    return true;
                }
                catch (SocketException e)
                {
                    Console.WriteLine("Connection Failed: " + e.Message);
                    stream = null;
                    client = null;
                    return false;
                }
            }

            public string recieveMessage()
            {
                try
                {
                    if (stream == null || !stream.DataAvailable)
                        return null;

                    byte[] receiveBuffer = new byte[1024];
                    int bytesReceived = stream.Read(receiveBuffer, 0, receiveBuffer.Length);

                    if (bytesReceived == 0)
                        return null;

                    Console.WriteLine(bytesReceived);
                    string data = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
                    Console.WriteLine(data);
                    return data;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Receive failed: " + e.Message);
                    return null;
                }
            }

            public void sendMessage(string message)
            {
                try
                {
                    if (stream == null)
                        return;

                    byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                    stream.Write(sendBuffer, 0, sendBuffer.Length);
                    stream.Flush();

                    Console.WriteLine("Sent: " + message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Send failed: " + e.Message);
                }
            }
        }
        public static void Main(string[] argv)
        {
            int port = 0;
            switch (argv.Length)
            {
                case 1:
                    port = int.Parse(argv[0], null);
                    if (port == 0) goto default;
                    break;
                case 0:
                    port = 3333;
                    break;
                default:
                    Console.WriteLine("usage: mono TuioDemo [port]");
                    Environment.Exit(0);
                    break;
            }

            TuioDemo app = new TuioDemo(port);
            Application.Run(app);
        }
    }
}