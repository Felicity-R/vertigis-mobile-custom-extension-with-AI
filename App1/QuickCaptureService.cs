using App1;
using Esri.ArcGISRuntime.Geometry;
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

        public QuickCaptureService(CommonAppDependencies deps)
        {
            _ops = deps.Operations;
            _dialog = deps.DialogController;
            _mapRepo = deps.MapRepo;

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

                EnhancedFileData photoData;

                // Take a photo or pick a file
                var choice = await _dialog.ShowConfirmationDialogAsync("Take a photo, or choose an existing photo from your library", "Select", "Use camera", "Choose from library");
                if (choice)
                {
                    photoData = await _ops.PhotoOperations.TakePhoto.ExecuteAsync();
                }
                else
                {
                    var args = new PickFileArgs(AttachmentType.Media);
                    photoData = await _ops.FileOperations.PickFile.ExecuteAsync(args);
                }

                // Process photo using AI service
                // TODO

                // Use AI results to populate some feature attribute(s)
                var attributes = new Dictionary<string, object?>();
                // TODO
                attributes["Name"] = "TODO Name this tree";

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
                var attachmentArgs = new AddAttachmentArgs(photoData, [vertiGISFeature], map);
                await _ops.EditOperations.AddAttachment.ExecuteAsync(attachmentArgs);

                // Launch the feature editing form so user can tweak values if necessary
                await _ops.EditOperations.DisplayUpdateFeature.ExecuteAsync(vertiGISFeature);
            }
            catch (Exception e)
            {
                await _dialog.ShowAlertAsync($"{e.Message}", "Error");
                // TODO: remove feature if a failure happens part way through, after adding it to the table
            }
        }
    }
}
