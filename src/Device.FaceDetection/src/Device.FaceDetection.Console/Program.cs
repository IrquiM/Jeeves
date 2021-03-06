﻿using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace Device.FaceDetection
{
    class Program
    {
        const string SubscriptionKey = "TODO: Add Key"; // Key 1 or Key 2 from the Face API, not the subscription id
        const string FaceEndpoint = "https://northeurope.api.cognitive.microsoft.com";

        static void Main(string[] args)
        {
            FaceClient faceClient = new FaceClient(new ApiKeyServiceClientCredentials(SubscriptionKey), new DelegatingHandler[] { });
            faceClient.Endpoint = FaceEndpoint;

            var capture = new VideoCapture(0); // Specifies the camera unit (by index)
            //var capture = new VideoCapture("http://172.25.95.176:8081"); // Stream from Pi

            var classifier = new CascadeClassifier("haarcascade_frontalface_default.xml");
            int snapshotInterval = 1000; // Take one snapshot from the video feed every x milliseconds

            using (var window = new Window("capture"))
            {
                var image = new Mat();
                while (true)
                {
                    capture.Read(image);
                    if (image.Empty())
                    {
                        break;
                    }

                    var (faceDetected, faceMat) = DetectFace(classifier, image);
                    window.ShowImage(faceMat);

                    //var faceDetected = ImageContainsFaces(image, classifier);

                    if (faceDetected)
                    {
                        using (var stream = new MemoryStream())
                        {
                            image.WriteToStream(stream);
                            stream.Flush();
                            stream.Seek(0, SeekOrigin.Begin);

                            var detectionResult = faceClient.Face.DetectWithStreamAsync(stream, true, false,
                                new List<FaceAttributeType>
                                {
                                    FaceAttributeType.Gender,
                                    FaceAttributeType.Glasses,
                                    FaceAttributeType.Age,
                                    FaceAttributeType.Smile
                                }).GetAwaiter().GetResult();

                            foreach (var detectedFace in detectionResult)
                            {
                                Console.WriteLine($"We detected someone. They are {detectedFace.FaceAttributes.Gender.GetValueOrDefault(Gender.Genderless)}, at age {detectedFace.FaceAttributes.Age} and {detectedFace.FaceAttributes.Smile} happy");
                            }
                        }
                    }

                    Cv2.WaitKey(snapshotInterval);
                }
            }
        }

        private static bool ImageContainsFaces(Mat srcImage, CascadeClassifier classifier)
        {
            using (var gray = new Mat())
            {
                Cv2.CvtColor(srcImage, gray, ColorConversionCodes.BGR2GRAY);
                return classifier.DetectMultiScale(gray, 1.08, 2, HaarDetectionType.ScaleImage, new Size(30, 30)).Length > 0;
            }
        }

        private static (bool FaceDetected, Mat Mat) DetectFace(CascadeClassifier cascade, Mat src)
        {
            Mat result;
            bool faceDetected;

            using (var gray = new Mat())
            {
                result = src.Clone();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

                // Detect faces
                Rect[] faces = cascade.DetectMultiScale(
                    gray, 1.08, 2, HaarDetectionType.ScaleImage, new Size(30, 30));

                faceDetected = faces.Length > 0;
                // Render all detected faces
                foreach (Rect face in faces)
                {
                    var center = new Point
                    {
                        X = (int)(face.X + face.Width * 0.5),
                        Y = (int)(face.Y + face.Height * 0.5)
                    };
                    var axes = new Size
                    {
                        Width = (int)(face.Width * 0.5),
                        Height = (int)(face.Height * 0.5)
                    };

                    Cv2.Ellipse(result, center, axes, 0, 0, 360, new Scalar(255, 0, 255), 4);
                }
            }

            return (faceDetected, result);
        }
    }
}
