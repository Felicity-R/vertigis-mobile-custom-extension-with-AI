using App1;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
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

            try
            {
                _openAIAssistant = new OpenAIAssistant();
            }
            catch (InvalidOperationException ex)
            {
                _dialog.ShowAlertAsync(ex).NoWait();
                return;
            }

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
                position = HandleZAndMValues(table, position);

                EnhancedFileData fileData = await GetPhotoFromUser();

                await _ops.UIOperations.DisplayBusyState.ExecuteAsync();

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
                Dictionary<string, object?> attributes = GetAttributesFromResponse(response.Content[0].Text);

                // Create the new feature
                var vertiGISFeature = await GetNewFeature(layerExt, table, position, attributes);

                // Add the photo as an attachment on the feature
                var attachmentArgs = new AddAttachmentArgs(fileData, [vertiGISFeature], map);
                await _ops.EditOperations.AddAttachment.ExecuteAsync(attachmentArgs);

                await _ops.ResultsOperations.DisplayDetails.ExecuteAsync(vertiGISFeature);

                // Launch the feature editing form so user can tweak values if necessary
                await _ops.EditOperations.DisplayUpdateFeature.ExecuteAsync(vertiGISFeature);
            }
            catch (Exception e)
            {
                await _dialog.ShowAlertAsync($"{e.Message}", "Error");
            }
            finally
            {
                await _ops.UIOperations.HideBusyState.ExecuteAsync();
            }
        }

        private static async Task<VertiGIS.ArcGISExtensions.Data.Feature> GetNewFeature(VertiGIS.ArcGISExtensions.Mapping.LayerExtension layerExt, FeatureTable table, MapPoint position, Dictionary<string, object?> attributes)
        {
            var newFeature = table.CreateFeature(attributes, position);
            await table.AddFeatureAsync(newFeature);

            if (table is ServiceFeatureTable serviceFeatureTable)
            {
                // If we're editing an online feature, apply edits and get the submitted feature
                var editResults = await serviceFeatureTable.ApplyEditsAsync();
                var qp = new QueryParameters() { ReturnGeometry = true };
                qp.ObjectIds.Add(editResults.First().ObjectId);
                newFeature = (await table.QueryFeaturesAsync(qp)).First();
            }

            // Get the VertiGIS representation of the new feature
            var vertiGISFeature = newFeature.ToVertiGISFeature(layerExt);
            return vertiGISFeature;
        }

        private static Dictionary<string, object?> GetAttributesFromResponse(string responseText)
        {
            var temp = JsonSerializer.Deserialize<Dictionary<string, string>>(responseText)!;

            var attributes = new Dictionary<string, object?>();

            foreach (var kvp in temp)
            {
                attributes.Add(kvp.Key, kvp.Value);
            }

            return attributes;
        }

        private async Task<EnhancedFileData> GetPhotoFromUser()
        {
            EnhancedFileData fileData;
            // Take a photo or pick a file
            var choice = await _dialog.ShowConfirmationDialogAsync("Take a photo, or choose an existing photo from your library", "Select", "Use camera", "Choose from library");
            if (choice)
            {
                fileData = await _ops.PhotoOperations.TakePhoto.ExecuteAsync();
            }
            else
            {
                var ops = PickOptions.Images;
                var picked = await FilePicker.Default.PickAsync(ops);
                fileData = new EnhancedFileData(picked);
            }

            return fileData;
        }

        private static MapPoint? HandleZAndMValues(FeatureTable table, MapPoint? position)
        {
            if (!table.HasZ)
            {
                position = position?.RemoveZ() as MapPoint;
            }
            if (!table.HasM)
            {
                position = position?.RemoveM() as MapPoint;
            }

            return position;
        }
    }
}
