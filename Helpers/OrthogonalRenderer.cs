using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TreatmentPlanReport.Helpers
{
    public static class OrthogonalRenderer
    {
        /// <summary>
        /// Generates a transverse (axial) slice image at the specified center point.
        /// Includes CT image, structure contours, dose overlay, and patient orientation indicator.
        /// </summary>
        /// <param name="plan">The planning item (PlanSetup or PlanSum) to render.</param>
        /// <param name="center">The 3D point in patient coordinates to center the slice on.</param>
        /// <param name="structuresToRender">List of structures to draw as contours.</param>
        /// <param name="includeDose">Whether to overlay the dose distribution.</param>
        /// <returns>A WriteableBitmap containing the rendered transverse image.</returns>
        public static WriteableBitmap GetTransverseImage(PlanningItem plan,
            VVector center,
            List<VMS.TPS.Common.Model.API.Structure> structuresToRender,
            bool includeDose)
        {
            var image = plan.StructureSet.Image;

            if (image == null)
                return null;

            // Get image dimensions
            int xSize = image.XSize;
            int ySize = image.YSize;
            int zSize = image.ZSize;

            // Check if we need to flip horizontally for FeetFirstSupine
            bool flipHorizontal = (image.ImagingOrientation == PatientOrientation.FeetFirstSupine);

            // Calculate which slice to render based on the center point
            // Convert center point from patient coordinates to slice index
            var imageOrigin = image.Origin;
            var zDirection = image.ZDirection;
            double zRes = image.ZRes;

            // Calculate distance along Z direction from origin to center point
            var originToCenter = center - imageOrigin;
            double zDistance = originToCenter.x * zDirection.x +
                             originToCenter.y * zDirection.y +
                             originToCenter.z * zDirection.z;

            // Convert to slice index
            int sliceIndex = (int)Math.Round(zDistance / zRes);
            sliceIndex = Math.Max(0, Math.Min(zSize - 1, sliceIndex)); // Clamp to valid range

            // Create voxel buffer
            int[,] voxels = new int[xSize, ySize];

            // Get voxels for the transverse slice
            image.GetVoxels(sliceIndex, voxels);

            // Convert Hounsfield Units (HU) to grayscale using soft tissue window settings
            int windowCenter = image.Level;
            int windowWidth = image.Window;

            int minValue = windowCenter - windowWidth / 2;
            int maxValue = windowCenter + windowWidth / 2;

            // Create WriteableBitmap with BGR32 format (8 bits per channel, 32 bits total)
            WriteableBitmap bitmap = new WriteableBitmap(
                xSize,
                ySize,
                96,
                96,
                PixelFormats.Bgr32,
                null);

            // Lock the bitmap for writing
            bitmap.Lock();

            try
            {
                unsafe
                {
                    int* pBackBuffer = (int*)bitmap.BackBuffer;
                    int stride = bitmap.BackBufferStride / 4; // Convert stride to int units

                    // Convert voxel HU values to grayscale BGR32 pixels
                    for (int y = 0; y < ySize; y++)
                    {
                        for (int x = 0; x < xSize; x++)
                        {
                            int voxelValue = voxels[x, y];

                            // Apply windowing to convert HU to display value (0-255)
                            int displayValue = (voxelValue - minValue) * 255 / (maxValue - minValue);
                            displayValue = Math.Max(0, Math.Min(255, displayValue));

                            byte grayValue = (byte)displayValue;

                            // Apply horizontal flip for FeetFirstSupine orientation
                            int pixelX = flipHorizontal ? (xSize - 1 - x) : x;
                            int pixelIndex = y * stride + pixelX;

                            // Set BGR32 pixel (blue, green, red channels all equal for grayscale)
                            pBackBuffer[pixelIndex] = (grayValue << 16) | (grayValue << 8) | grayValue;
                        }
                    }

                    // Convert int pointer to byte pointer for drawing operations
                    byte* pBackBufferBytes = (byte*)pBackBuffer;
                    int byteStride = bitmap.BackBufferStride;

                    // Overlay structure contours
                    if (structuresToRender != null && structuresToRender.Any())
                    {
                        DrawStructuresOnSlice(pBackBufferBytes, byteStride, xSize, ySize,
                            structuresToRender, image, sliceIndex, flipHorizontal);
                    }

                    // Overlay dose distribution with color wash
                    if (includeDose && plan.Dose != null)
                    {
                        plan.DoseValuePresentation = DoseValuePresentation.Absolute;
                        double rxDose = CalculatePrescriptionDose(plan);
                        
                        DrawDoseOverlay(pBackBufferBytes, byteStride, xSize, ySize,
                            plan.Dose, image, rxDose, sliceIndex, flipHorizontal);
                        DrawDoseLegend(pBackBufferBytes, byteStride, xSize, ySize, rxDose);
                    }

                    // Draw patient orientation indicator ("cosmonaut")
                    DrawCosmonaut(pBackBufferBytes, byteStride, xSize, ySize, plan, image, "Transverse");
                }

                // Mark the entire bitmap as dirty
                bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, xSize, ySize));
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }

        /// <summary>
        /// Generates a sagittal slice image at the specified center point.
        /// Shows a left-right view through the patient.
        /// </summary>
        /// <param name="plan">The planning item to render.</param>
        /// <param name="center">The 3D point in patient coordinates to center the slice on.</param>
        /// <param name="structuresToRender">List of structures to draw as contours.</param>
        /// <param name="includeDose">Whether to overlay the dose distribution.</param>
        /// <returns>A WriteableBitmap containing the rendered sagittal image.</returns>
        public static WriteableBitmap GetSagittalImage(PlanningItem plan,
            VVector center,
            List<VMS.TPS.Common.Model.API.Structure> structuresToRender,
            bool includeDose)
        {
            var image = plan.StructureSet.Image;

            if (image == null)
                return null;

            // Get image dimensions - for sagittal: height=Z, width=Y
            int xSize = image.XSize;
            int ySize = image.YSize;
            int zSize = image.ZSize;
            bool flipVertical = (image.ImagingOrientation != PatientOrientation.FeetFirstSupine);
            // Calculate which sagittal slice (X coordinate) to render
            var imageOrigin = image.Origin;
            var xDirection = image.XDirection;
            double xRes = image.XRes;

            var originToCenter = center - imageOrigin;
            double xDistance = originToCenter.x * xDirection.x +
                             originToCenter.y * xDirection.y +
                             originToCenter.z * xDirection.z;

            int sliceIndex = (int)Math.Round(xDistance / xRes);
            sliceIndex = Math.Max(0, Math.Min(xSize - 1, sliceIndex));

            // For sagittal plane: width=Y size, height=Z size
            int imageWidth = ySize;
            int imageHeight = zSize;

            // Create voxel buffer for sagittal slice
            int[,] voxels = new int[imageHeight, imageWidth]; // [Z, Y]

            // Extract voxels for sagittal plane (constant X)
            for (int z = 0; z < zSize; z++)
            {
                int[,] transverseSlice = new int[xSize, ySize];
                image.GetVoxels(z, transverseSlice);
                for (int y = 0; y < ySize; y++)
                {
                    voxels[z, y] = transverseSlice[sliceIndex, y];
                }
            }

            // Convert HU to grayscale using soft tissue window settings
            int windowCenter = image.Level;
            int windowWidth = image.Window;

            int minValue = windowCenter - windowWidth / 2;
            int maxValue = windowCenter + windowWidth / 2;

            // Create WriteableBitmap for sagittal view
            WriteableBitmap bitmap = new WriteableBitmap(
                imageWidth,
                imageHeight,
                96,
                96,
                PixelFormats.Bgr32,
                null);
            bitmap.Lock();

            try
            {
                unsafe
                {
                    byte* pBackBuffer = (byte*)bitmap.BackBuffer;
                    int stride = bitmap.BackBufferStride;

                    // Convert voxel values to BGR32 pixels with proper flipping
                    for (int z = 0; z < imageHeight; z++)
                    {
                        for (int y = 0; y < imageWidth; y++)
                        {
                            // Apply vertical flip based on patient orientation
                            int voxelValue = flipVertical ? voxels[imageHeight - 1 - z, y] : voxels[z, y];

                            // Apply windowing
                            int displayValue = (voxelValue - minValue) * 255 / (maxValue - minValue);
                            displayValue = Math.Max(0, Math.Min(255, displayValue));

                            byte grayValue = (byte)displayValue;

                            // Apply horizontal flip (left-right mirror)
                            int pixelOffset = z * stride + (imageWidth - 1 - y) * 4;

                            pBackBuffer[pixelOffset] = grayValue;
                            pBackBuffer[pixelOffset + 1] = grayValue;
                            pBackBuffer[pixelOffset + 2] = grayValue;
                            pBackBuffer[pixelOffset + 3] = 255;
                        }
                    }

                    // Overlay structure contours on sagittal view
                    if (structuresToRender != null && structuresToRender.Any())
                    {
                        DrawStructuresOnSagittalSlice(pBackBuffer, stride, imageWidth, imageHeight,
                            structuresToRender, image, sliceIndex, flipVertical);
                    }

                    // Overlay dose distribution
                    if (includeDose && plan.Dose != null)
                    {
                        double rxDose = CalculatePrescriptionDose(plan);
                        DrawDoseOverlaySagittal(pBackBuffer, stride, imageWidth, imageHeight,
                            plan.Dose, image, rxDose, sliceIndex, flipVertical);
                        DrawDoseLegend(pBackBuffer, stride, imageWidth, imageHeight, rxDose);
                    }

                    // Draw patient orientation indicator
                    DrawCosmonaut(pBackBuffer, stride, imageWidth, imageHeight, plan, image, "Sagittal");
                }

                bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, imageWidth, imageHeight));
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }

        /// <summary>
        /// Generates a frontal (coronal) slice image at the specified center point.
        /// Shows an anterior-posterior view through the patient.
        /// </summary>
        /// <param name="plan">The planning item to render.</param>
        /// <param name="center">The 3D point in patient coordinates to center the slice on.</param>
        /// <param name="structuresToRender">List of structures to draw as contours.</param>
        /// <param name="includeDose">Whether to overlay the dose distribution.</param>
        /// <returns>A WriteableBitmap containing the rendered frontal image.</returns>
        public static WriteableBitmap GetFrontalImage(PlanningItem plan,
            VVector center,
            List<VMS.TPS.Common.Model.API.Structure> structuresToRender,
            bool includeDose)
        {
            var image = plan.StructureSet.Image;

            if (image == null)
                return null;

            // Get image dimensions - for frontal/coronal: height=Z, width=X
            int xSize = image.XSize;
            int ySize = image.YSize;
            int zSize = image.ZSize;

            // Calculate which frontal slice (Y coordinate) to render
            var imageOrigin = image.Origin;
            var yDirection = image.YDirection;
            double yRes = image.YRes;

            var originToCenter = center - imageOrigin;
            double yDistance = originToCenter.x * yDirection.x +
                     originToCenter.y * yDirection.y +
                     originToCenter.z * yDirection.z;

            int sliceIndex = (int)Math.Round(yDistance / yRes);
            sliceIndex = Math.Max(0, Math.Min(ySize - 1, sliceIndex));

            // For frontal plane: width=X size, height=Z size
            int imageWidth = xSize;
            int imageHeight = zSize;
            // Check if we need to flip horizontally for FeetFirstSupine
            bool flipHorizontal = (image.ImagingOrientation == PatientOrientation.FeetFirstSupine);
            bool flipVertical = (image.ImagingOrientation != PatientOrientation.FeetFirstSupine);
            // Create voxel buffer for frontal slice
            int[,] voxels = new int[imageHeight, imageWidth]; // [Z, X]

            // Extract voxels for frontal plane (constant Y)
            for (int z = 0; z < zSize; z++)
            {
                int[,] transverseSlice = new int[xSize, ySize];
                image.GetVoxels(z, transverseSlice);
                for (int x = 0; x < xSize; x++)
                {
                    voxels[z, x] = transverseSlice[x, sliceIndex];
                }
            }

            // Convert HU to grayscale using soft tissue window settings
            int windowCenter = image.Level;
            int windowWidth = image.Window;

            int minValue = windowCenter - windowWidth / 2;
            int maxValue = windowCenter + windowWidth / 2;

            // Create WriteableBitmap for frontal view
            WriteableBitmap bitmap = new WriteableBitmap(
                imageWidth,
                imageHeight,
                96,
                96,
                PixelFormats.Bgr32,
                null);
            bitmap.Lock();

            try
            {
                unsafe
                {
                    byte* pBackBuffer = (byte*)bitmap.BackBuffer;
                    int stride = bitmap.BackBufferStride;

                    // Convert voxel values to BGR32 pixels with proper flipping
                    for (int z = 0; z < imageHeight; z++)
                    {
                        for (int x = 0; x < imageWidth; x++)
                        {
                            // Apply vertical flip based on patient orientation
                            int voxelValue = flipVertical ? voxels[imageHeight - 1 - z, x] : voxels[z, x];

                            // Apply windowing
                            int displayValue = (voxelValue - minValue) * 255 / (maxValue - minValue);
                            displayValue = Math.Max(0, Math.Min(255, displayValue));

                            byte grayValue = (byte)displayValue;

                            // Flip horizontally (left-right)
                            int pixelX = flipHorizontal ? (imageWidth - 1 - x) : x;
                            int pixelOffset = z * stride + pixelX * 4;

                            pBackBuffer[pixelOffset] = grayValue;
                            pBackBuffer[pixelOffset + 1] = grayValue;
                            pBackBuffer[pixelOffset + 2] = grayValue;
                            pBackBuffer[pixelOffset + 3] = 255;
                        }
                    }

                    // Overlay structure contours on frontal view
                    if (structuresToRender != null && structuresToRender.Any())
                    {
                        DrawStructuresOnFrontalSlice(pBackBuffer, stride, imageWidth, imageHeight,
                            structuresToRender, image, sliceIndex, flipHorizontal, flipVertical);
                    }

                    // Overlay dose distribution
                    if (includeDose && plan.Dose != null)
                    {
                        double rxDose = CalculatePrescriptionDose(plan);
                        DrawDoseOverlayFrontal(pBackBuffer, stride, imageWidth, imageHeight,
                            plan.Dose, image, rxDose, sliceIndex, flipHorizontal, flipVertical);
                        DrawDoseLegend(pBackBuffer, stride, imageWidth, imageHeight, rxDose);
                    }

                    // Draw patient orientation indicator
                    DrawCosmonaut(pBackBuffer, stride, imageWidth, imageHeight, plan, image, "Coronal");
                }

                bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, imageWidth, imageHeight));
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }

        /// <summary>
        /// Calculates the prescription dose for a plan or plan sum.
        /// For plan sums, determines whether plans are at same location (sum doses) or different locations (max dose).
        /// </summary>
        private static double CalculatePrescriptionDose(PlanningItem plan)
        {
            if (plan is PlanSetup planSetup)
            {
                return planSetup.TotalDose.Dose;
            }
            else if (plan is PlanSum planSum)
            {
                double maxDose = plan.Dose.DoseMax3D.Dose;
                double summedDose = planSum.PlanSetups.Sum(ps => ps.TotalDose.Dose);

                // If max dose exceeds sum, plans are at the same location
                if (maxDose > summedDose)
                {
                    return summedDose;
                }
                else
                {
                    // Plans are at different locations, use highest individual dose
                    return planSum.PlanSetups.Max(ps => ps.TotalDose.Dose);
                }
            }

            return plan.Dose.DoseMax3D.Dose;
        }

        /// <summary>
        /// Draws structure contours on a sagittal slice.
        /// </summary>
        /// <summary>
        /// Draws structure contours on a sagittal slice.
        /// </summary>
        private static unsafe void DrawStructuresOnSagittalSlice(byte* pBackBuffer, int stride, int width, int height,
            List<Structure> structures, Image image, int xSliceIndex, bool flipVertical)
        {
            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            foreach (var structure in structures)
            {
                if (structure == null || structure.IsEmpty)
                    continue;

                byte colorB = structure.Color.B;
                byte colorG = structure.Color.G;
                byte colorR = structure.Color.R;
                byte colorA = 255;

                // Iterate through all Z slices to find contours that intersect the sagittal plane
                for (int z = 0; z < image.ZSize; z++)
                {
                    var contoursOnSlice = structure.GetContoursOnImagePlane(z);

                    if (contoursOnSlice == null || contoursOnSlice.Length == 0)
                        continue;

                    int displayZ = flipVertical ? height - 1 - z : z;

                    foreach (var contour in contoursOnSlice)
                    {
                        if (contour == null || contour.Length < 2)
                            continue;

                        for (int i = 0; i < contour.Length; i++)
                        {
                            var point1 = contour[i];
                            var point2 = contour[(i + 1) % contour.Length];

                            // Convert patient coordinates to image coordinates
                            var p1ToOrigin = point1 - origin;
                            var p2ToOrigin = point2 - origin;

                            double x1_img = (p1ToOrigin.x * xDir.x + p1ToOrigin.y * xDir.y + p1ToOrigin.z * xDir.z) / xRes;
                            double y1_img = (p1ToOrigin.x * yDir.x + p1ToOrigin.y * yDir.y + p1ToOrigin.z * yDir.z) / yRes;
                            double x2_img = (p2ToOrigin.x * xDir.x + p2ToOrigin.y * xDir.y + p2ToOrigin.z * xDir.z) / xRes;
                            double y2_img = (p2ToOrigin.x * yDir.x + p2ToOrigin.y * yDir.y + p2ToOrigin.z * yDir.z) / yRes;

                            // Check if contour line segment crosses the sagittal plane
                            if ((x1_img <= xSliceIndex && x2_img >= xSliceIndex) ||
                                (x1_img >= xSliceIndex && x2_img <= xSliceIndex))
                            {
                                // Interpolate Y position where the line crosses the slice
                                double t = Math.Abs(x2_img - x1_img) < 0.001 ? 0.5 : (xSliceIndex - x1_img) / (x2_img - x1_img);
                                int yPixel = (int)(y1_img + t * (y2_img - y1_img));

                                // Draw intersection point
                                if (yPixel >= 0 && yPixel < width && z >= 0 && z < height)
                                {
                                    int pixelOffset = displayZ * stride + (width - 1 - yPixel) * 4;
                                    pBackBuffer[pixelOffset] = colorB;
                                    pBackBuffer[pixelOffset + 1] = colorG;
                                    pBackBuffer[pixelOffset + 2] = colorR;
                                    pBackBuffer[pixelOffset + 3] = colorA;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws structure contours on a frontal (coronal) slice.
        /// </summary>
        private static unsafe void DrawStructuresOnFrontalSlice(byte* pBackBuffer, int stride, int width, int height,
            List<Structure> structures, Image image, int ySliceIndex,
            bool flipHorizontal = false, bool flipVertical = true)
        {
            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            foreach (var structure in structures)
            {
                if (structure == null || structure.IsEmpty)
                    continue;

                byte colorB = structure.Color.B;
                byte colorG = structure.Color.G;
                byte colorR = structure.Color.R;
                byte colorA = 255;

                // Iterate through all Z slices and extract X-Z contours at the specified Y position
                for (int z = 0; z < image.ZSize; z++)
                {
                    var contoursOnSlice = structure.GetContoursOnImagePlane(z);

                    if (contoursOnSlice == null || contoursOnSlice.Length == 0)
                        continue;

                    foreach (var contour in contoursOnSlice)
                    {
                        if (contour == null || contour.Length < 2)
                            continue;

                        for (int i = 0; i < contour.Length; i++)
                        {
                            var point1 = contour[i];
                            var point2 = contour[(i + 1) % contour.Length];

                            var p1ToOrigin = point1 - origin;
                            var p2ToOrigin = point2 - origin;

                            double x1_img = (p1ToOrigin.x * xDir.x + p1ToOrigin.y * xDir.y + p1ToOrigin.z * xDir.z) / xRes;
                            double y1_img = (p1ToOrigin.x * yDir.x + p1ToOrigin.y * yDir.y + p1ToOrigin.z * yDir.z) / yRes;
                            double x2_img = (p2ToOrigin.x * xDir.x + p2ToOrigin.y * xDir.y + p2ToOrigin.z * xDir.z) / xRes;
                            double y2_img = (p2ToOrigin.x * yDir.x + p2ToOrigin.y * yDir.y + p2ToOrigin.z * yDir.z) / yRes;

                            // Check if line segment crosses the frontal plane
                            if ((y1_img <= ySliceIndex && y2_img >= ySliceIndex) ||
                                (y1_img >= ySliceIndex && y2_img <= ySliceIndex))
                            {
                                // Interpolate X position at the slice
                                double t = Math.Abs(y2_img - y1_img) < 0.001 ? 0.5 : (ySliceIndex - y1_img) / (y2_img - y1_img);
                                int xPixel = (int)(x1_img + t * (x2_img - x1_img));

                                // Draw point on frontal image (X stays as X, Z stays as Y) - flipped vertically
                                if (xPixel >= 0 && xPixel < width && z >= 0 && z < height)
                                {
                                    // Flip vertically to match flipped CT image
                                    int displayZ = flipVertical ? height - 1 - z : z;
                                    int displayX = flipHorizontal ? (width - 1 - xPixel) : xPixel;
                                    int pixelOffset = displayZ * stride + displayX * 4;
                                    pBackBuffer[pixelOffset] = colorB;
                                    pBackBuffer[pixelOffset + 1] = colorG;
                                    pBackBuffer[pixelOffset + 2] = colorR;
                                    pBackBuffer[pixelOffset + 3] = colorA;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws dose overlay on a sagittal slice with color wash and transparency.
        /// </summary>
        private static unsafe void DrawDoseOverlaySagittal(byte* pBackBuffer, int stride, int width, int height,
            Dose dose, VMS.TPS.Common.Model.API.Image image, double rxDose, int xSliceIndex, bool flipVertical = true)
        {
            if (dose == null)
                return;

            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            for (int z = 0; z < height; z++)
            {
                for (int y = 0; y < width; y++)
                {
                    // Flip Z coordinate to match the flipped CT image display
                    int displayZ = flipVertical ? height - 1 - z : z;

                    // Convert sagittal pixel coordinates to patient coordinates
                    VVector pixelPos = origin +
                        (xDir * xSliceIndex * xRes) +
                        (yDir * y * yRes) +
                        (zDir * displayZ * zRes);

                    var doseValue = dose.GetDoseToPoint(pixelPos);

                    if (doseValue.Dose > 0)
                    {
                        double totalDose = rxDose;

                        if (doseValue.Dose >= 0.60 * rxDose)
                        {
                            // Flip horizontally to match flipped CT image
                            int pixelOffset = z * stride + (width - 1 - y) * 4;

                            byte doseR, doseG, doseB;
                            GetDoseColor(doseValue.Dose, totalDose, out doseR, out doseG, out doseB);

                            // Alpha blend with existing pixel (40% transparency for dose)
                            float alpha = 0.4f;
                            pBackBuffer[pixelOffset] = (byte)((doseB * alpha) + (pBackBuffer[pixelOffset] * (1 - alpha)));
                            pBackBuffer[pixelOffset + 1] = (byte)((doseG * alpha) + (pBackBuffer[pixelOffset + 1] * (1 - alpha)));
                            pBackBuffer[pixelOffset + 2] = (byte)((doseR * alpha) + (pBackBuffer[pixelOffset + 2] * (1 - alpha)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws dose overlay on a frontal (coronal) slice with color wash and transparency.
        /// </summary>
        private static unsafe void DrawDoseOverlayFrontal(byte* pBackBuffer, int stride, int width, int height,
            Dose dose, Image image, double rxDose, int ySliceIndex,
            bool flipHorizontal = false, bool flipVertical = true)
        {
            if (dose == null)
                return;

            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int actualZ = flipVertical ? height - 1 - z : z;

                    // Convert pixel coordinates to patient coordinates
                    VVector pixelPos = origin +
                        (xDir * x * xRes) +
                        (yDir * ySliceIndex * yRes) +
                        (zDir * actualZ * zRes);

                    var doseValue = dose.GetDoseToPoint(pixelPos);

                    // Only render dose above 60% threshold
                    if (doseValue.Dose >= 0.60 * rxDose)
                    {
                        int pixelX = flipHorizontal ? (width - 1 - x) : x;
                        int pixelOffset = z * stride + pixelX * 4;

                        byte doseR, doseG, doseB;
                        GetDoseColor(doseValue.Dose, rxDose, out doseR, out doseG, out doseB);

                        // Alpha blend with 40% transparency
                        float alpha = 0.4f;
                        pBackBuffer[pixelOffset] = (byte)((doseB * alpha) + (pBackBuffer[pixelOffset] * (1 - alpha)));
                        pBackBuffer[pixelOffset + 1] = (byte)((doseG * alpha) + (pBackBuffer[pixelOffset + 1] * (1 - alpha)));
                        pBackBuffer[pixelOffset + 2] = (byte)((doseR * alpha) + (pBackBuffer[pixelOffset + 2] * (1 - alpha)));
                    }
                }
            }
        }

        /// <summary>
        /// Draws a patient orientation indicator ("cosmonaut") on the image.
        /// </summary>
        private static unsafe void DrawCosmonaut(byte* pBackBuffer, int stride, int xSize, int ySize, PlanningItem plan, Image image, string orientation)
        {
            try
            {
                // Load the cosmonaut image from embedded resources
                BitmapSource cosmonautImage = LoadCosmonautImage(orientation, image.ImagingOrientation);

                if (cosmonautImage == null)
                    return;

                // Resize the cosmonaut image to approximately 40x40 pixels
                int targetSize = 40;
                double scaleX = targetSize / (double)cosmonautImage.PixelWidth;
                double scaleY = targetSize / (double)cosmonautImage.PixelHeight;

                // Use the smaller scale to maintain aspect ratio and fit within 40x40
                double scale = Math.Min(scaleX, scaleY);

                TransformedBitmap resizedBitmap = new TransformedBitmap();
                resizedBitmap.BeginInit();
                resizedBitmap.Source = cosmonautImage;
                resizedBitmap.Transform = new ScaleTransform(scale, scale);
                resizedBitmap.EndInit();

                cosmonautImage = resizedBitmap;

                // Convert to BGR32 format if needed
                if (cosmonautImage.Format != PixelFormats.Bgr32)
                {
                    cosmonautImage = new FormatConvertedBitmap(cosmonautImage, PixelFormats.Bgr32, null, 0);
                }

                int cosmoWidth = cosmonautImage.PixelWidth;
                int cosmoHeight = cosmonautImage.PixelHeight;

                // Position in bottom right corner with some padding
                int padding = 10;
                int startX = xSize - cosmoWidth - padding;
                int startY = ySize - cosmoHeight - padding;

                // Ensure we don't go out of bounds
                if (startX < 0 || startY < 0)
                    return;

                // Copy pixel data from cosmonaut image
                int cosmoStride = cosmoWidth * 4; // 4 bytes per pixel for BGR32
                byte[] cosmoPixels = new byte[cosmoHeight * cosmoStride];
                cosmonautImage.CopyPixels(cosmoPixels, cosmoStride, 0);

                // Draw cosmonaut image onto the main image buffer
                for (int y = 0; y < cosmoHeight; y++)
                {
                    for (int x = 0; x < cosmoWidth; x++)
                    {
                        int destX = startX + x;
                        int destY = startY + y;

                        // Check bounds
                        if (destX < 0 || destX >= xSize || destY < 0 || destY >= ySize)
                            continue;

                        // Get pixel from cosmonaut image
                        int cosmoOffset = y * cosmoStride + x * 4;
                        byte b = cosmoPixels[cosmoOffset];
                        byte g = cosmoPixels[cosmoOffset + 1];
                        byte r = cosmoPixels[cosmoOffset + 2];
                        byte a = cosmoPixels[cosmoOffset + 3];

                        // Calculate destination offset
                        int destOffset = destY * stride + destX * 4;

                        // Alpha blend the cosmonaut image
                        if (a > 0)
                        {
                            float alpha = a / 255.0f;
                            pBackBuffer[destOffset] = (byte)((b * alpha) + (pBackBuffer[destOffset] * (1 - alpha)));
                            pBackBuffer[destOffset + 1] = (byte)((g * alpha) + (pBackBuffer[destOffset + 1] * (1 - alpha)));
                            pBackBuffer[destOffset + 2] = (byte)((r * alpha) + (pBackBuffer[destOffset + 2] * (1 - alpha)));
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if cosmonaut image cannot be loaded
            }
        }

        /// <summary>
        /// Loads the appropriate cosmonaut (patient orientation indicator) image from embedded resources.
        /// Selects the correct orientation based on view direction and patient position.
        /// </summary>
        /// <param name="direction">The view direction (Transverse, Sagittal, or Coronal).</param>
        /// <param name="orientation">The patient orientation (e.g., HeadFirstSupine).</param>
        /// <returns>A BitmapSource of the cosmonaut image, or null if not found.</returns>
        private static BitmapSource LoadCosmonautImage(string direction, PatientOrientation orientation)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourcePrefix = "TreatmentPlanReport.resources";
                string imageOrientation = String.Empty;
                bool flipImage = false;

                // Determine the appropriate cosmonaut orientation based on view and patient position
                if (direction == "Transverse")
                {
                    switch (orientation)
                    {
                        case PatientOrientation.HeadFirstSupine:
                            imageOrientation = "HFS";
                            break;
                        case PatientOrientation.HeadFirstProne:
                            imageOrientation = "HFP";
                            break;
                        case PatientOrientation.FeetFirstSupine:
                            imageOrientation = "FFS";
                            break;
                        case PatientOrientation.FeetFirstProne:
                            imageOrientation = "FFP";
                            break;

                    }

                }
                else if (direction == "Sagittal")
                {
                    switch (orientation)
                    {
                        case PatientOrientation.HeadFirstSupine:
                            imageOrientation = "Right";
                            break;
                        case PatientOrientation.HeadFirstProne:
                            imageOrientation = "Left";
                            break;
                        case PatientOrientation.FeetFirstSupine:
                            imageOrientation = "Left";
                            flipImage = true;
                            break;
                        case PatientOrientation.FeetFirstProne:
                            imageOrientation = "Right";
                            flipImage = true;
                            break;

                    }
                }
                else if (direction == "Coronal")
                {
                    switch (orientation)
                    {
                        case PatientOrientation.HeadFirstSupine:
                            imageOrientation = "Anterior";
                            break;
                        case PatientOrientation.HeadFirstProne:
                            imageOrientation = "Posterior";
                            break;
                        case PatientOrientation.FeetFirstSupine:
                            imageOrientation = "Anterior";
                            flipImage = true;

                            break;
                        case PatientOrientation.FeetFirstProne:
                            imageOrientation = "Posterior";
                            flipImage = true;
                            break;

                    }
                }

                // Find matching resource from embedded resources
                string[] resourceNames = assembly.GetManifestResourceNames();
                string directionLower = direction.ToLower();
                string orientationLower = imageOrientation.ToLower();

                string targetResource = resourceNames.FirstOrDefault(name =>
                    name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) &&
                    name.ToLower().Contains(directionLower) &&
                    name.ToLower().Contains(orientationLower));

                // DEBUG: Uncomment to see what was found
                // System.Diagnostics.Debug.WriteLine($"Found resource: {targetResource ?? "NULL"}");

                if (string.IsNullOrEmpty(targetResource))
                    return null;

                // Load and decode the image from the embedded resource
                using (Stream stream = assembly.GetManifestResourceStream(targetResource))
                {
                    if (stream == null)
                        return null;

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();

                    // Flip image 180 degrees if needed for FeetFirst orientations
                    if (flipImage)
                    {
                        TransformedBitmap flippedBitmap = new TransformedBitmap();
                        flippedBitmap.BeginInit();
                        flippedBitmap.Source = bitmap;
                        flippedBitmap.Transform = new RotateTransform(180);
                        flippedBitmap.EndInit();
                        flippedBitmap.Freeze();
                        return flippedBitmap;
                    }

                    bitmap.Freeze(); // Make it thread-safe

                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Draws structure contours on a transverse slice.
        /// Uses Bresenham's line algorithm to connect contour points.
        /// </summary>
        private static unsafe void DrawStructuresOnSlice(byte* pBackBuffer, int stride, int width, int height,
            List<Structure> structures, Image image, int sliceIndex, bool flipHorizontal = false)
        {
            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            foreach (var structure in structures)
            {
                if (structure == null || structure.IsEmpty)
                    continue;

                byte colorB = structure.Color.B;
                byte colorG = structure.Color.G;
                byte colorR = structure.Color.R;
                byte colorA = 255;

                var contoursOnSlice = structure.GetContoursOnImagePlane(sliceIndex);

                if (contoursOnSlice == null || contoursOnSlice.Length == 0)
                    continue;

                foreach (var contour in contoursOnSlice)
                {
                    if (contour == null || contour.Length < 2)
                        continue;

                    for (int i = 0; i < contour.Length; i++)
                    {
                        var point1 = contour[i];
                        var point2 = contour[(i + 1) % contour.Length];

                        var p1ToOrigin = point1 - origin;
                        var p2ToOrigin = point2 - origin;

                        int x1 = (int)((p1ToOrigin.x * xDir.x + p1ToOrigin.y * xDir.y + p1ToOrigin.z * xDir.z) / xRes);
                        int y1 = (int)((p1ToOrigin.x * yDir.x + p1ToOrigin.y * yDir.y + p1ToOrigin.z * yDir.z) / yRes);
                        int x2 = (int)((p2ToOrigin.x * xDir.x + p2ToOrigin.y * xDir.y + p2ToOrigin.z * xDir.z) / xRes);
                        int y2 = (int)((p2ToOrigin.x * yDir.x + p2ToOrigin.y * yDir.y + p2ToOrigin.z * yDir.z) / yRes);

                        // Flip horizontally if needed
                        if (flipHorizontal)
                        {
                            x1 = width - 1 - x1;
                            x2 = width - 1 - x2;
                        }

                        DrawLine(pBackBuffer, stride, width, height, x1, y1, x2, y2,
                            colorB, colorG, colorR, colorA);
                    }
                }
            }
        }

        /// <summary>
        /// Draws dose overlay on a transverse slice with color wash and transparency.
        /// Only renders dose above 60% of prescription dose.
        /// </summary>
        private static unsafe void DrawDoseOverlay(byte* pBackBuffer, int stride, int width, int height,
            Dose dose, Image image, double rxDose, int sliceIndex, bool flipHorizontal = false)
        {
            if (dose == null)
                return;

            double xRes = image.XRes;
            double yRes = image.YRes;
            double zRes = image.ZRes;
            var origin = image.Origin;
            var xDir = image.XDirection;
            var yDir = image.YDirection;
            var zDir = image.ZDirection;

            // Sample dose at each pixel position
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Convert pixel coordinates to patient coordinates
                    VVector pixelPos = origin +
                        (xDir * x * xRes) +
                        (yDir * y * yRes) +
                        (zDir * sliceIndex * zRes);

                    var doseValue = dose.GetDoseToPoint(pixelPos);
                    if (Double.IsNaN(doseValue.Dose))
                    {
                        continue;
                    }
                    // Only render dose above 60% threshold
                    //System.Windows.MessageBox.Show(doseValue.ToString());
                    if (doseValue.Dose >= 0.60 * rxDose)
                    {
                        int pixelX = flipHorizontal ? (width - 1 - x) : x;
                        int pixelOffset = y * stride + pixelX * 4;

                        byte doseR, doseG, doseB;
                        GetDoseColor(doseValue.Dose, rxDose, out doseR, out doseG, out doseB);

                        // Alpha blend with 40% transparency
                        float alpha = 0.4f;
                        pBackBuffer[pixelOffset] = (byte)((doseB * alpha) + (pBackBuffer[pixelOffset] * (1 - alpha)));
                        pBackBuffer[pixelOffset + 1] = (byte)((doseG * alpha) + (pBackBuffer[pixelOffset + 1] * (1 - alpha)));
                        pBackBuffer[pixelOffset + 2] = (byte)((doseR * alpha) + (pBackBuffer[pixelOffset + 2] * (1 - alpha)));
                    }
                }
            }
        }

        /// <summary>
        /// Converts a dose value to an RGB color based on standard radiation therapy color wash.
        /// Color scheme: Red (=105%) ? Yellow (100-105%) ? Green (95-100%) ? Blue (90-95%) ? Indigo (80-90%) ? Violet (60-80%).
        /// </summary>
        private static void GetDoseColor(double dose, double totalDose, out byte r, out byte g, out byte b)
        {
            // Calculate ratio of dose to total dose
            double ratio = dose / totalDose;

            // Standard dose colorwash: Red -> Yellow -> Green -> Blue -> Indigo -> Violet
            if (ratio >= 1.05)
            {
                // Red (=105%)
                r = 255;
                g = 0;
                b = 0;
            }
            else if (ratio >= 1.00)
            {
                // Yellow (100-105%)
                r = 255;
                g = 255;
                b = 0;
            }
            else if (ratio >= 0.95)
            {
                // Green (95-100%)
                r = 0;
                g = 255;
                b = 0;
            }
            else if (ratio >= 0.90)
            {
                // Blue (90-95%)
                r = 0;
                g = 0;
                b = 255;
            }
            else if (ratio >= 0.80)
            {
                // Indigo (80-90%)
                r = 75;
                g = 0;
                b = 130;
            }
            else if (ratio >= 0.60)
            {
                // Violet (60-80%)
                r = 148;
                g = 0;
                b = 211;
            }
            else
            {
                // Dark Purple (<60%)
                r = 75;
                g = 0;
                b = 75;
            }
        }

        /// <summary>
        /// Draws a line between two points using Bresenham's line algorithm.
        /// </summary>
        private static unsafe void DrawLine(byte* pBackBuffer, int stride, int width, int height,
            int x0, int y0, int x1, int y1, byte b, byte g, byte r, byte a)
        {
            // Bresenham's line algorithm
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int x = x0;
            int y = y0;

            while (true)
            {
                // Draw pixel if within bounds
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    int pixelOffset = y * stride + x * 4;
                    pBackBuffer[pixelOffset] = b;     // Blue
                    pBackBuffer[pixelOffset + 1] = g; // Green
                    pBackBuffer[pixelOffset + 2] = r; // Red
                    pBackBuffer[pixelOffset + 3] = a; // Alpha
                }

                if (x == x1 && y == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        /// <summary>
        /// Draws a dose legend showing color-coded dose levels and their percentage values.
        /// Displayed in the top-left corner of the image.
        /// </summary>
        private static unsafe void DrawDoseLegend(byte* pBackBuffer, int stride, int width, int height, double rxDose)
        {
            if (rxDose <= 0)
                return;

            // Legend configuration
            int legendX = 10;
            int legendY = 10;
            int legendWidth = 120;
            int lineHeight = 18;
            int colorBoxSize = 12;
            int padding = 5;

            // Define dose levels and colors relative to rxDose
            var doseLevels = new[]
            {
    (ratio: 1.05, label: "105%", color: (R: 255, G: 0, B: 0)),       // Red
    (ratio: 1.00, label: "100%", color: (R: 255, G: 255, B: 0)),    // Yellow
    (ratio: 0.95, label: "95%", color: (R: 0, G: 255, B: 0)),       // Green
    (ratio: 0.90, label: "90%", color: (R: 0, G: 0, B: 255)),       // Blue
    (ratio: 0.80, label: "80%", color: (R: 75, G: 0, B: 130)),      // Indigo
    (ratio: 0.60, label: "60%", color: (R: 148, G: 0, B: 211))      // Violet
};

            int legendHeight = doseLevels.Length * lineHeight + padding * 2 + 15;

            // Draw semi-transparent background
            byte bgR = 40, bgG = 40, bgB = 40, bgA = 100;
            for (int y = legendY; y < legendY + legendHeight && y < height; y++)
            {
                for (int x = legendX; x < legendX + legendWidth && x < width; x++)
                {
                    int pixelOffset = y * stride + x * 4;
                    float alpha = bgA / 255.0f;
                    pBackBuffer[pixelOffset] = (byte)((bgB * alpha) + (pBackBuffer[pixelOffset] * (1 - alpha)));
                    pBackBuffer[pixelOffset + 1] = (byte)((bgG * alpha) + (pBackBuffer[pixelOffset + 1] * (1 - alpha)));
                    pBackBuffer[pixelOffset + 2] = (byte)((bgR * alpha) + (pBackBuffer[pixelOffset + 2] * (1 - alpha)));
                }
            }

            // Draw title
            DrawSimpleText(pBackBuffer, stride, width, height, legendX + padding, legendY + padding, "Dose Wash");

            // Draw dose level entries
            for (int i = 0; i < doseLevels.Length; i++)
            {
                var level = doseLevels[i];
                int entryY = legendY + padding + 15 + i * lineHeight;

                // Draw color box
                for (int y = entryY; y < entryY + colorBoxSize && y < height; y++)
                {
                    for (int x = legendX + padding; x < legendX + padding + colorBoxSize && x < width; x++)
                    {
                        int pixelOffset = y * stride + x * 4;
                        pBackBuffer[pixelOffset] = (byte)level.color.B;
                        pBackBuffer[pixelOffset + 1] = (byte)level.color.G;
                        pBackBuffer[pixelOffset + 2] = (byte)level.color.R;
                        pBackBuffer[pixelOffset + 3] = 255;
                    }
                }

                // Draw dose value text
                double doseValue = level.ratio * rxDose;
                string doseText = $"{level.label} ({doseValue:F0}cGy)";
                DrawSimpleText(pBackBuffer, stride, width, height,
                    legendX + padding + colorBoxSize + 5, entryY + 2, doseText);
            }
        }

        /// <summary>
        /// Draws simple text using bitmap font patterns.
        /// </summary>
        private static unsafe void DrawSimpleText(byte* pBackBuffer, int stride, int width, int height,
            int x, int y, string text)
        {
            byte textR = 255, textG = 255, textB = 255;

            for (int i = 0; i < text.Length && x < width; i++)
            {
                DrawChar(pBackBuffer, stride, width, height, x, y, text[i], textB, textG, textR);
                x += 6;
            }
        }

        /// <summary>
        /// Draws a single character using a predefined bitmap pattern.
        /// </summary>
        private static unsafe void DrawChar(byte* pBackBuffer, int stride, int width, int height,
            int startX, int startY, char c, byte b, byte g, byte r)
        {
            bool[,] pattern = GetCharPattern(c);

            if (pattern == null)
                return;

            for (int y = 0; y < pattern.GetLength(0) && (startY + y) < height; y++)
            {
                for (int x = 0; x < pattern.GetLength(1) && (startX + x) < width; x++)
                {
                    if (pattern[y, x] && startX + x >= 0 && startY + y >= 0)
                    {
                        int pixelOffset = (startY + y) * stride + (startX + x) * 4;
                        pBackBuffer[pixelOffset] = b;
                        pBackBuffer[pixelOffset + 1] = g;
                        pBackBuffer[pixelOffset + 2] = r;
                        pBackBuffer[pixelOffset + 3] = 255;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a 7x5 bitmap pattern for a specific character.
        /// Used for rendering text on images without font dependencies.
        /// </summary>
        /// <param name="c">The character to get the pattern for.</param>
        /// <returns>A 2D boolean array representing the character's pixels, or null if not found.</returns>
        private static bool[,] GetCharPattern(char c)
        {
            switch (c)
            {
                case '0':
                    return new bool[,] {
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, true, true},
        {true, false, true, false, true},
        {true, true, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case '1':
                    return new bool[,] {
        {false, false, true, false, false},
        {false, true, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, true, true, true, false}
    };
                case '2':
                    return new bool[,] {
        {false, true, true, true, false},
        {true, false, false, false, true},
        {false, false, false, false, true},
        {false, false, false, true, false},
        {false, false, true, false, false},
        {false, true, false, false, false},
        {true, true, true, true, true}
    };
                case '3':
                    return new bool[,] {
        {true, true, true, true, true},
        {false, false, false, true, false},
        {false, false, true, false, false},
        {false, false, false, true, false},
        {false, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case '4':
                    return new bool[,] {
        {false, false, false, true, false},
        {false, false, true, true, false},
        {false, true, false, true, false},
        {true, false, false, true, false},
        {true, true, true, true, true},
        {false, false, false, true, false},
        {false, false, false, true, false}
    };
                case '5':
                    return new bool[,] {
        {true, true, true, true, true},
        {true, false, false, false, false},
        {true, true, true, true, false},
        {false, false, false, false, true},
        {false, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case '6':
                    return new bool[,] {
        {false, false, true, true, false},
        {false, true, false, false, false},
        {true, false, false, false, false},
        {true, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case '7':
                    return new bool[,] {
        {true, true, true, true, true},
        {false, false, false, false, true},
        {false, false, false, true, false},
        {false, false, true, false, false},
        {false, true, false, false, false},
        {false, true, false, false, false},
        {false, true, false, false, false}
    };
                case '8':
                    return new bool[,] {
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case '9':
                    return new bool[,] {
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, true},
        {false, false, false, false, true},
        {false, false, false, true, false},
        {false, true, true, false, false}
    };
                case '%':
                    return new bool[,] {
        {true, true, false, false, true},
        {true, true, false, true, false},
        {false, false, true, false, false},
        {false, true, false, false, false},
        {false, true, false, true, true},
        {true, false, false, true, true},
        {true, false, false, false, false}
    };
                case '(':
                    return new bool[,] {
        {false, false, true, false, false},
        {false, true, false, false, false},
        {false, false, false, false, false},
        {false, true, false, false, false},
        {false, true, false, false, false},
        {false, true, false, false, false},
        {false, false, true, false, false}
    };
                case ')':
                    return new bool[,] {
        {false, true, false, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, false, true, false, false},
        {false, true, false, false, false}
    };
                case 'c':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, true, true, true, false},
        {true, false, false, false, false},
        {true, false, false, false, false},
        {true, false, false, false, false},
        {false, true, true, true, false}
    };
                case 'G':
                    return new bool[,] {
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, false},
        {true, false, true, true, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case 'y':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, true},
        {false, false, false, false, true},
        {false, true, true, true, false}
    };
                case 'D':
                    return new bool[,] {
        {true, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, true, true, true, false}
    };
                case 'o':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {false, true, true, true, false}
    };
                case 's':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, true, true, true, false},
        {true, false, false, false, false},
        {false, true, true, true, false},
        {false, false, false, false, true},
        {true, true, true, true, false}
    };
                case 'e':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, true, true, true, false},
        {true, false, false, false, true},
        {true, true, true, true, true},
        {true, false, false, false, false},
        {false, true, true, true, false}
    };
                case 'W':
                    return new bool[,] {
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, true, false, true},
        {true, false, true, false, true},
        {true, true, false, true, true},
        {true, false, false, false, true}
    };
                case 'a':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, true, true, true, false},
        {false, false, false, false, true},
        {false, true, true, true, true},
        {true, false, false, false, true},
        {false, true, true, true, true}
    };
                case 'h':
                    return new bool[,] {
        {true, false, false, false, false},
        {true, false, false, false, false},
        {true, true, true, true, false},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true},
        {true, false, false, false, true}
    };
                case ' ':
                    return new bool[,] {
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, false, false, false, false},
        {false, false, false, false, false}
    };
                default: return new bool[5, 7];
            }
        }
    }
}
