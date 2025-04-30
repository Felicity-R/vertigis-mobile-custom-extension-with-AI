using App1;
using Esri.ArcGISRuntime.Geometry;
using Microsoft.Maui.Graphics.Platform;
using System.Text.Json;
using VertiGIS.ArcGISExtensions.Utilities;
using VertiGIS.Mobile.Composition;
using VertiGIS.Mobile.Composition.Messaging;
using VertiGIS.Mobile.Composition.Services;
using VertiGIS.Mobile.Infrastructure.App;
using VertiGIS.Mobile.Infrastructure.Dialog;
using VertiGIS.Mobile.Infrastructure.Maps;
using VertiGIS.Mobile.Infrastructure.Messaging;
using VertiGIS.Mobile.Toolkit.File;
using VertiGIS.Mobile.Toolkit.Utilities;

[assembly: Service(typeof(QuickCaptureService))]
namespace App1
{
    public class QuickCaptureService : ServiceBase
    {
        private AllOperations _ops;
        private IDialogController _dialog;
        private MapRepository _mapRepo;
        private OpenAIAssistant _openAIAssistant;

        public QuickCaptureService(CommonAppDependencies deps)
        {
            _ops = deps.Operations;
            _dialog = deps.DialogController;
            _mapRepo = deps.MapRepo;
            _openAIAssistant = new OpenAIAssistant();

            // Register our custom command. This is called by name later from the "I Want To..." menu.
            deps.OperationRegistry.VoidOperation("custom.quick-capture").RegisterExecute(DoQuickCaptureAsync, this);
        }

        private async Task DoQuickCaptureAsync()
        {
            try
            {
                // Get the map (the first/only available map), and the layer we want to add to
                var map = _mapRepo.AllMaps.EnumerateExisting().First().MapExtension;
                var layerExt = map.LayerExtensions.FindByLayerId("1968288a255-layer-2"); // 1968288a255-layer-2 = the trees layer
                var table = layerExt.GetFeatureTable();

                // Get current location (will place feature there)
                var position = await _ops.GeolocationOperations.GetPosition.ExecuteAsync();
                if (!table.HasZ)
                {
                    position = position.RemoveZ() as MapPoint;
                }
                if (!table.HasM)
                {
                    position = position.RemoveM() as MapPoint;
                }

                EnhancedFileData fileData;
                FileResult? photo;

                // Take a photo or pick a file
                var choice = await _dialog.ShowConfirmationDialogAsync("Take a photo, or choose an existing photo from your library", "Select", "Use camera", "Choose from library");
                if (choice)
                {
                    photo = await TakePhotoAsync();
                    fileData = new EnhancedFileData(photo);
                }
                else
                {
                    var ops = PickOptions.Images;
                    photo = await FilePicker.Default.PickAsync(ops);
                    fileData = new EnhancedFileData(photo);
                }

                // Use the fields calculated here.
                var fieldJson = FieldUtils.GetFieldsAsStringifiedJSON(table.Fields);

                /*
                 *  Example of how to use OpenAIClient to query a picture. TODO: Parse response text into feature
                 */
                var queries = new List<string>
                    {
                        """
                        Fill out the following information about a specific tree, from the given image.
                        {
                            CommonName:
                            ScientificName:
                            Family:
                            ConservationStatus:
                            Health:
                        }
                        'Health' should be an evaluation of the health of the individual tree pictured.
                        Respond only with JSON.
                        """,
                    };

                var response = await _openAIAssistant.QueryImageAsync(fileData.Data, queries);
                var responseText = response.Content[0].Text;

                var temp = JsonSerializer.Deserialize<Dictionary<string, string>>(responseText)!;

                var attributes = new Dictionary<string, object?>();

                foreach(var kvp in temp)
                {
                    attributes.Add(kvp.Key, kvp.Value);
                }

                // Create the new feature
                var newFeature = table.CreateFeature(attributes, position);
                await table.AddFeatureAsync(newFeature);

                if (table is Esri.ArcGISRuntime.Data.ServiceFeatureTable serviceFeatureTable)
                {
                    // If we're editing an online feature, apply edits and get the submitted feature
                    var editResults = await serviceFeatureTable.ApplyEditsAsync();
                    var qp = new Esri.ArcGISRuntime.Data.QueryParameters() { ReturnGeometry = true };
                    qp.ObjectIds.Add(editResults.First().ObjectId);
                    newFeature = (await table.QueryFeaturesAsync(qp)).First();
                }

                // Get the VertiGIS representation of the new feature
                var vertiGISFeature = newFeature.ToVertiGISFeature(layerExt);

                // Add the photo as an attachment on the feature
                var attachmentArgs = new AddAttachmentArgs(fileData, [vertiGISFeature], map);
                await _ops.EditOperations.AddAttachment.ExecuteAsync(attachmentArgs);

                // Launch the feature editing form so user can tweak values if necessary
                await _ops.EditOperations.DisplayUpdateFeature.ExecuteAsync(vertiGISFeature);
            }
            catch (Exception e)
            {
                await _dialog.ShowAlertAsync($"{e.Message}", "Error");
                // TODO: remove feature if a failure happens part way through, after adding it to the table

                // Test
            }
        }

        private static async Task<FileResult?> TakePhotoAsync()
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    return photo;
                }
            }

            return null;
        }

        private static async Task<byte[]> GetPhotoAsBytesAsync(FileResult photo)
        {
            if (photo == null)
            {
                return Array.Empty<byte>();
            }

            using var stream = await photo.OpenReadAsync();
            var image = PlatformImage.FromStream(stream);

            // Default photos are around 4 MB, downsize by 1/4 on Android.
            if (image.Height > 1008 || image.Width > 1008)
            {
                var newImage = image.Downsize(1008f, false);
                return newImage.AsBytes();
            }

            return image.AsBytes();
        }
    }
}
