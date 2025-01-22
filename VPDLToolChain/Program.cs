using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using ViDi2;
using ViDi2.Training.Local;
 
//Must be run on an x64 platform.
//NuGet packages can be found C:\ProgramData\Cognex\VisionPro Deep Learning\x\Examples\packages
namespace ToolChainTraining
{
    internal class Program
    {
        static void Main(string[] args)
        {          
            //Initialize workspace directory
            ViDi2.Training.Local.WorkspaceDirectory workspaceDir = new ViDi2.Training.Local.WorkspaceDirectory();
            //Set the path to workspace directory
            workspaceDir.Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Training";

            using (LibraryAccess libraryAccess = new LibraryAccess(workspaceDir))
            {
                //Create a control interface for training tools
                using (ViDi2.Training.IControl myControl = new ViDi2.Training.Local.Control(libraryAccess))
                {
                    myControl.OptimizedGPUMemory(6 * 1024 * 1024 * 1024ul);

                    //Create a new workspace and add it to the control
                    ViDi2.Training.IWorkspace myWorkspace = myControl.Workspaces.Add("myToolChainWorkspace");

                    //Add a new stream to the workspace
                    ViDi2.Training.IStream myStream = myWorkspace.Streams.Add("default");

                    //Add a Blue Tool to the stream
                    ViDi2.Training.IBlueTool myBlueTool = myStream.Tools.Add("Locate", ViDi2.ToolType.Blue) as ViDi2.Training.IBlueTool;

                    //Define valid image file extensions
                    List<string> ext = new List<string> { ".jpg", ".bmp", ".png" };
                    //Get all image files in the specified directory that match the extensions
                    IEnumerable<string> imageFiles = System.IO.Directory.GetFiles(
                        Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName + "\\Images",
                        //@"C:\...\Images",
                        "*.*",
                        System.IO.SearchOption.TopDirectoryOnly
                    ).Where(s => ext.Any(e => s.EndsWith(e)));

                    //Add to the tool database.
                    foreach (string imageFile in imageFiles)
                    {
                        using (ViDi2.FormsImage image = new ViDi2.FormsImage(imageFile))
                        {
                            myStream.Database.AddImage(image, Path.GetFileName(imageFile));
                        }
                    }

                    //Process all images in the Blue Tool's database
                    myBlueTool.Database.Process();

                    //Wait until the processing is done
                    myBlueTool.Wait();

                    //Set various parameters for the Blue Tool
                    myBlueTool.Parameters.FeatureSize = new ViDi2.Size(185, 225);
                    myBlueTool.Parameters.Rotation = new List<ViDi2.Interval> { new ViDi2.Interval(0, 2 * Math.PI) };
                    myBlueTool.Parameters.ScaledFeatures = true;
                    myBlueTool.Parameters.OrientedFeatures = true;
                    myBlueTool.Parameters.Luminance = 0.05;
                    myBlueTool.Parameters.Contrast = 0.05;
                    myBlueTool.Parameters.CountEpochs = 5;

                    //Load an xml file containing training data
                    XmlDocument doc = new XmlDocument();
                    doc.Load(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName + "\\BlueToolLabelisation.xml");
                    //doc.Load(@"C:\...\BlueToolLabelisation.xml");

                    //Loop through each node in the xml document, add and set features accordingly
                    //Each node has this format.
                    /*
                    <?xml version="1.0"?>
                    <Views>
                        <View view_id="bad_defect (1).png">
                            <Pos_x>255,0</Pos_x>
                            <Pos_y>258,1</Pos_y>
                            <Angle>0,34732052115</Angle>
                            <Width>194,1</Width>
                            <Height>232,6</Height>
                        </View>
                    .
                    .
                    .
                    </Views>
                     */
                    foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                    {
                        //List<ViDi2.Training.SortedViewKey> list = myBlueTool.Database.List().ToList();
                        //ViDi2.Training.ViewKey sample = list.Find(m => m.SampleName == node.Attributes[0].Value) as ViDi2.Training.ViewKey;

                        //ViDi2.Point point = new ViDi2.Point(double.Parse(node.ChildNodes[0].InnerText), Double.Parse(node.ChildNodes[1].InnerText));
                        //double angle = Double.Parse(node.ChildNodes[2].InnerText);
                        //double width = Double.Parse(node.ChildNodes[3].InnerText);
                        //double height = Double.Parse(node.ChildNodes[4].InnerText);
                        //ViDi2.Size size = new ViDi2.Size(width, height);

                        //myBlueTool.Database.AddFeature(sample, "object", point, angle, 1.0);
                        //myBlueTool.Database.SetFeature(sample, 0, "object", point, angle, 1.0);

                        // VPDL-QA-JK added the modified code
                        List<ViDi2.Training.SortedViewKey> list = myBlueTool.Database.List().ToList();
                        ViDi2.Training.ViewKey sample = list.Find(m => m.SampleName == node.Attributes[0].Value) as ViDi2.Training.ViewKey;
                        //node.ChildNodes[0].InnerXml.Replace(',', '.'); // <Pos_x>255,0</Pos_x> --> <Pos_x>255.0</Pos_x> --> Pos_x: 255.0
                        ViDi2.Point point = new ViDi2.Point(double.Parse(node.ChildNodes[0].InnerXml.Replace(',', '.')), Double.Parse(node.ChildNodes[1].InnerXml.Replace(',', '.')));
                        double angle = Double.Parse(node.ChildNodes[2].InnerXml.Replace(',', '.'));
                        double width = Double.Parse(node.ChildNodes[3].InnerXml.Replace(',', '.'));
                        double height = Double.Parse(node.ChildNodes[4].InnerXml.Replace(',', '.'));
                        ViDi2.Size size = new ViDi2.Size(width, height);
                        myBlueTool.Database.AddFeature(sample, "object", point, angle, 1.0);
                        myBlueTool.Database.SetFeature(sample, 0, "object", point, angle, size);
                    }

                    //Mark the dataset as ready for training
                    myBlueTool.Database.SetTrainFlag("", true);

                    //Start training the Blue Tool
                    myBlueTool.Train();
                    Console.WriteLine("Starting:");

                    //Monitor the progress of the training
                    while (!myBlueTool.Wait(1000))
                    {
                        Console.WriteLine(myBlueTool.Progress.Description + " " + myBlueTool.Progress.ETA.ToString());
                    }

                    //Process the database again after training
                    myBlueTool.Database.Process();
                    myBlueTool.Wait();

                    //Chain a Red Tool
                    //Add a Red Tool to the stream
                    ViDi2.Training.IRedTool myRedTool = myBlueTool.Children.Add("Analyze", ViDi2.ToolType.RedLegacy) as ViDi2.Training.IRedTool;
                    myRedTool.Database.Process();
                    myRedTool.Wait();

                    ////Set various parameters for the Red Tool
                    myRedTool.Database.LabelViews("'good'", "");
                    myRedTool.Database.LabelViews("'bad'", "defect");
                    myRedTool.Parameters.NetworkModel = "supervised/small";

                    myRedTool.Parameters.ColorChannels = 1;
                    myRedTool.Parameters.FeatureSize = new ViDi2.Size(25, 25);
                    myRedTool.Parameters.Luminance = 0.05;
                    myRedTool.Parameters.SamplingDensity = 4;
                    myRedTool.Parameters.CountEpochs = 40;
                    myRedTool.Parameters.AutoThresholdOn = true;

                    Dictionary<string, double> dictionary = new Dictionary<string, double>
                    {
                        { "defect", 0.4 } // Add the key "a" with the value 3.2
                    };
                    myRedTool.Parameters.RegionThresholds = dictionary;

                    //List all defect masks for Red Tool
                    //Load defect masks.
                    List<ViDi2.Training.SortedViewKey> imgList = myRedTool.Database.List("'bad'").ToList();
                    IEnumerable<string> regionImages = Directory.GetFiles(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName + "\\regions");
                    
                    myRedTool.Database.Process();
                    myRedTool.Wait();

                    //Loop through each defect mask and apply it to the Red Tool
                    foreach (string file in regionImages)
                    {
                        using (ViDi2.FormsImage region = new FormsImage(file))
                        {
                            List<ViDi2.Training.SortedViewKey> list = myRedTool.Database.List().ToList();
                            ViDi2.Training.SortedViewKey targetImg = imgList.FirstOrDefault(element => element.SampleName == Path.GetFileName(file));

                            if (targetImg != null)
                            {
                                ViDi2.Training.ViewKey viewKey = list.Find(m => m.SampleName == targetImg.SampleName);
                                ViDi2.IImage imask = region as ViDi2.IImage;

                                try
                                {
                                    Console.Write("Add region: " + Path.GetFileName(file).ToString());
                                    myRedTool.Database.SetRegionsImage(viewKey, "defect", imask);
                                    //System.Drawing.Bitmap temp = myRedTool.Database.GetRegionsImage(viewKey, "defect").Bitmap;
                                    //temp.Save(@"C:\Users\acooper\Desktop\Test\myImage.bmp");
                                    Console.WriteLine(Path.GetFileName(file).ToString());

                                }
                                catch (ViDi2.Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                        }
                    }

                    //Process Red Tool
                    myRedTool.Database.SelectTrainingSet("", 0.5);
                    myRedTool.Database.Process();
                    myRedTool.Wait();
                    //myRedTool.Database.SelectTrainingSet("", 0.75);

                    myRedTool.Database.SetTrainFlag("", true);//////////////////////////////////////////
                    //Train the Red Tool
                    myRedTool.Train();

                    //Monitor the progress of the training
                    while (!myRedTool.Wait(1000))
                    {
                        Console.WriteLine(myRedTool.Progress.Description + " " + myRedTool.Progress.ETA.ToString());
                    }

                    myRedTool.Database.Process();
                    myRedTool.Wait();

                    //Chain a Green Tool
                    //Add a Green Tool to the stream
                    ViDi2.Training.IGreenTool myGreenTool = myRedTool.Children.Add("Classify", ViDi2.ToolType.GreenLegacy) as ViDi2.Training.IGreenTool;
                    myGreenTool.Database.Process();
                    myGreenTool.Wait();

                    //Set various tool parameters
                    myGreenTool.Database.Tag("'defect'", "Defect");
                    myGreenTool.Database.Tag("'trace'", "Trace");
                    myGreenTool.Database.Tag("not labeled", "Good");
                    myGreenTool.Database.SelectTrainingSet("", 0.6);
                    myGreenTool.Parameters.FeatureSize = new ViDi2.Size(15, 15);
                    myGreenTool.Parameters.Rotation = new List<ViDi2.Interval>() { new ViDi2.Interval(-0.25 * Math.PI, 0.25 * Math.PI) };
                    myGreenTool.Parameters.CountEpochs = 50;

                    //Train the tool
                    myGreenTool.Train();

                    Console.Write("Start");

                    while (!myGreenTool.Wait(1000))
                    {
                        Console.WriteLine(myGreenTool.Progress.Description + " " + myGreenTool.Progress.ETA.ToString());
                    }

                    myGreenTool.Database.Process();
                    myGreenTool.Wait();

                    //Save the workspace
                    myWorkspace.Save();

                    //Export the runtime workspace to a file
                    using (System.IO.FileStream fs = new System.IO.FileStream(workspaceDir.Path + "\\ToolChainRuntime.vrws", System.IO.FileMode.Create))
                    {
                        myWorkspace.ExportRuntimeWorkspace().CopyTo(fs);
                    }
                    
                    myWorkspace.Close();
                    myControl.Dispose();
                }
            } 
        }
    }
}