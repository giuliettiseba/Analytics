using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using VideoOS.Platform.Data;
using VideoOS.Platform.Metadata;
using AForge.Imaging;

namespace AnalyticServiceProto
{
    class MetadataHandler
    {

        /// HTTP Server
        private MediaProviderService _metadataProviderService;
        private MetadataProviderChannel _metadataProviderChannel;
        private readonly MetadataSerializer _metadataSerializer = new MetadataSerializer();

        ///  Metadata
        private const int scaleArea = 50;

        // Maths
        private Dictionary<double, double> reciprocals = new Dictionary<double, double>();

        internal MetadataProviderChannel OpenHTTPService()
        {
            // Open the HTTP Service
            if (_metadataProviderService == null)
            {
                var hardwareDefinition = new HardwareDefinition(
                    PhysicalAddress.Parse("001122334455"),
                    "MetadataProvider")
                {
                    Firmware = "v10",
                    MetadataDevices = { MetadataDeviceDefintion.CreateBoundingBoxDevice() }
                };

                _metadataProviderService = new MediaProviderService();
                _metadataProviderService.Init(52123, "password", hardwareDefinition);
            }
            // Create a provider to handle channel 1
            _metadataProviderChannel = _metadataProviderService.CreateMetadataProvider(1);

            return _metadataProviderChannel;
        }

     


        private double Reciprocal(double val)
        {
            if (reciprocals.TryGetValue(val, out double reciprocal))
            {
                return reciprocal;
            }

            reciprocal = 1 / val;
            reciprocals[reciprocal] = val;
            return reciprocal;
        }


        private OnvifObject CreateOnvifObject(float x, float y, float area, string n, int id, int width, int height)
        {
            area /= scaleArea;
            float r_x = (float)Reciprocal(width);
            float r_y = (float)Reciprocal(height);
            float r_xx = r_x * 2;
            float r_yy = r_y * 2;
            var centerOfGravity = new Vector { X = x, Y = y };

            var blob = new OnvifObject(id)
            {
                Appearance = new VideoOS.Platform.Metadata.Appearance
                {
                    Shape = new Shape
                    {
                        BoundingBox = new VideoOS.Platform.Metadata.Rectangle
                        {
                            Bottom = height - y - area / 2,
                            Left = x - area / 2,
                            Top = height - y + area / 2,
                            Right = x + area / 2
                        },
                        CenterOfGravity = centerOfGravity
                    },
                    Description = new DisplayText
                    {
                        Value = n
                    },
                    Transformation = new Transformation
                    {
                        Translate = new Vector { X = -1, Y = -1 },
                        Scale = new Vector { X = r_xx, Y = r_yy }
                    }
                }
            };
            return blob;
        }

       

           internal string SendMetadataBox(Blob[] blobs, int w, int h)
        {
            try
            {
                OnvifObject blob1 = new OnvifObject();
                OnvifObject blob2 = new OnvifObject();
                OnvifObject blob3 = new OnvifObject();
                OnvifObject blob4 = new OnvifObject();

                if (blobs.Length > 1)
                    blob1 = CreateOnvifObject(blobs[0].CenterOfGravity.X, blobs[0].CenterOfGravity.Y, blobs[0].Area, blobs[0].ID.ToString(), 1,w,h);
                if (blobs.Length > 2)
                    blob2 = CreateOnvifObject(blobs[1].CenterOfGravity.X, blobs[1].CenterOfGravity.Y, blobs[1].Area, blobs[1].ID.ToString(), 2, w, h);
                if (blobs.Length > 3)
                    blob3 = CreateOnvifObject(blobs[2].CenterOfGravity.X, blobs[2].CenterOfGravity.Y, blobs[2].Area, blobs[2].ID.ToString(), 3, w, h);
                if (blobs.Length > 4)
                    blob4 = CreateOnvifObject(blobs[3].CenterOfGravity.X, blobs[3].CenterOfGravity.Y, blobs[3].Area, blobs[3].ID.ToString(), 4, w, h);

                MetadataStream metadata = new MetadataStream
                {
                    VideoAnalyticsItems =
                {
                    new VideoAnalytics
                    {
                        Frames =
                        {
                            new Frame(DateTime.UtcNow)
                            {
                                Objects =
                                {
                                         blob1,blob2,blob3,blob4
                                }
                            }
                        }
                    }
                }
                };

                var result = _metadataProviderChannel.QueueMetadata(metadata, DateTime.UtcNow);
                if (result == false)
                    return (string.Format("{0}: Failed to write to channel", DateTime.UtcNow));
                else
                {
                    return _metadataSerializer.WriteMetadataXml(metadata);
                 }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

    }
}
