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

                // Take a photo
                var photoData = await _ops.PhotoOperations.TakePhoto.ExecuteAsync();

                // Process photo using AI service
                // TODO

                // Use AI results to populate some feature attribute(s)
                var attributes = new Dictionary<string, object?>();
                // TODO

                // Create the new feature
                var newFeature = table.CreateFeature(attributes, position);
                await table.AddFeatureAsync(newFeature);
                var vertiGISFeature = newFeature.ToVertiGISFeature(layerExt);

                // Add the photo as an attachment on the feature
                var attachmentArgs = new AddAttachmentArgs(photoData, [vertiGISFeature], map);
                await _ops.EditOperations.AddAttachment.ExecuteAsync(attachmentArgs);

                // Launch the feature editing form so user can tweak values if necessary
                await _ops.EditOperations.DisplayUpdateFeature.ExecuteAsync(vertiGISFeature);

                // Confirmation (can remove later)
                await _dialog.ShowAlertAsync("Finished", "Success");
            }
            catch (Exception e)
            {
                await _dialog.ShowAlertAsync($"{e.Message}", "Error");
                // TODO: remove feature if a failure happens part way through, after adding it to the table
            }
        }
    }
}
