using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using MySqlConnector;
using System.Data.SqlClient;

namespace MultiFaceRec
{
    public partial class FrmPrincipal : Form
    {
        Capture grabber;
        HaarCascade face;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        int ContTrain, NumLabels, t;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        List<string> NamePersons = new List<string>();

        string CurrentStudent = "";
        Image<Bgr, Byte> currentFrame;
        Image<Gray, byte> result, TrainedFace = null;
        Image<Gray, byte> gray = null;

        private Timer timer = new Timer();

        string connectionString = "server=localhost;uid=root;pwd=;database=ams;";

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string log;
                ContTrain++;
                gray = grabber.QueryGrayFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                TrainedFace = DetectFace(gray);

                if (TrainedFace == null)
                {
                    MessageBox.Show("No face was detected.", "Registration Failed!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                trainingImages.Add(TrainedFace);

                if (string.IsNullOrEmpty(txtStudentID.Text))
                {
                    MessageBox.Show("Please enter a student ID.", "Registration Failed!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                labels.Add(txtStudentID.Text);
                imageBox1.Image = TrainedFace;

                if (!Directory.Exists("TrainedFaces")){ Directory.CreateDirectory("TrainedFaces");}

                File.WriteAllText($"{Application.StartupPath}/TrainedFaces/TrainedLabels.txt", $"{trainingImages.ToArray().Length}%");

                for (int i = 1; i <= trainingImages.ToArray().Length; i++)
                {
                    trainingImages.ToArray()[i - 1].Save($"{Application.StartupPath}/TrainedFaces/face{i}.bmp");
                    File.AppendAllText($"{Application.StartupPath}/TrainedFaces/TrainedLabels.txt", $"{labels.ToArray()[i - 1]}%");
                }

                MessageBox.Show($"Student: {txtStudentID.Text} registered successfully", "Registration Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                log = $"Student: {txtStudentID.Text} registered successfully";

                SaveDataToMySQL(txtStudentID.Text,txtName.Text);
                txtLog.Text = log;
                imageBox1.Image = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                MessageBox.Show("Enable the face detection first", "Registration Failed!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private Image<Gray, byte> DetectFace(Image<Gray, byte> gray)
        {
            try
            {
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(face, 1.2, 10, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(20, 20));

                foreach (MCvAvgComp f in facesDetected[0])
                {
                    TrainedFace = currentFrame.Copy(f.rect).Convert<Gray, byte>();
                    break;
                }

                return result.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return null;
            }
        }

        private void FrmPrincipal_Load(object sender, EventArgs e)
        {
            grabber = new Capture();
            grabber.QueryFrame();
            Application.Idle += new EventHandler(FrameGrabber);
        }

        public FrmPrincipal()
        {
            InitializeComponent();
            face = new HaarCascade("haarcascade_frontalface_default.xml");
            try
            {
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;
                string LoadFaces;

                for (int tf = 1; tf < NumLabels+1; tf++)
                {
                    LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);
                }
            }
            catch(Exception e)
            {
                MessageBox.Show("Nothing in binary database, please register a student.", "Trained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void FrameGrabber(object sender, EventArgs e)
        {
            label3.Text = "0";
            NamePersons.Add("");
            currentFrame = grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
            gray = currentFrame.Convert<Gray, Byte>();

            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(face, 1.2, 10, Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(20, 20));

            if (facesDetected[0].Length == 0)
            {
                listBoxNames.Items.Clear();
            }
            else
            {
                foreach (MCvAvgComp f in facesDetected[0])
                {
                    string name = "";
                    t = t + 1;
                    result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                    currentFrame.Draw(f.rect, new Bgr(Color.Red), 2);

                    if (trainingImages.ToArray().Length != 0)
                    {
                        MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);
                        EigenObjectRecognizer recognizer = new EigenObjectRecognizer(trainingImages.ToArray(), labels.ToArray(), 3000, ref termCrit);
                        name = recognizer.Recognize(result);
                        CurrentStudent = name;
                        currentFrame.Draw(name, ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));
                    }
                    NamePersons[t - 1] = name;
                    NamePersons.Add("");
                    label3.Text = facesDetected[0].Length.ToString();

                    // Add the recognized name to the ListBox
                    if (!string.IsNullOrEmpty(name) && !listBoxNames.Items.Contains(name))
                    {
                        listBoxNames.Items.Add(name);
                    }
                }
                if (facesDetected[0].Length > 0)
                {
                    string[] nameList = NamePersons.ToArray();
                    listBoxNames.SelectedItem = nameList[0];
                }
            }

            t = 0;
            imageBoxFrameGrabber.Image = currentFrame;
            NamePersons.Clear();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (listBoxNames.SelectedItem != null)
            {
                string selectedName = listBoxNames.SelectedItem.ToString();
                MessageBox.Show("Student: " + selectedName + " is now marked as PRESENT");
            }
            else
            {
                MessageBox.Show("No student selected");
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            lblDateTime.Text = DateTime.Now.ToString("MMMM dd, yyyy hh:mm:ss tt");
        }



        private void SaveDataToMySQL(string studentNumber, string name)
        { 
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = "INSERT INTO students (studentNumber, name) VALUES (@studentNumber, @name)";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@studentNumber", studentNumber);
                    command.Parameters.AddWithValue("@name", name);
                    command.ExecuteNonQuery();
                }
            }
        }

    }
}