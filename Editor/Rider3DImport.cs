using UnityEditor;

namespace BikeGame.EditorTools
{
    /// <summary>Настройки импорта 3D-гонщика (Resources/Rider3D/*.fbx): скелет без анимаций, кости не прячутся, метры.</summary>
    public class Rider3DImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Rider3D/") && !p.Contains("/Resources/RiderGear/")) return;   // гонщик и шлемы магазина
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.optimizeGameObjects = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;   // имена материалов нужны Rider3D
            mi.importNormals = ModelImporterNormals.Import;
            mi.isReadable = false;
        }
    }
}
