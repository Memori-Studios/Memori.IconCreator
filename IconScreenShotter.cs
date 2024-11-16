using System.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace Memori.IconCreator
{
public enum ImageFilterMode { Nearest, Bilinear, Average }
public class ScreenshotHandler : MonoBehaviour
{
    [SerializeField] private bool takeScreenShots;
    [SerializeField] private GameObject[] itemsToScreenShot;
    [SerializeField] private GameObject manualScreenShotObject;
    [SerializeField] private int2 resolution = new int2(512, 768);
    [SerializeField] private Camera myCamera;
    [SerializeField] private bool transparentBackground;
    [SerializeField] private string filepath = "Assets/Resources/Results/";

    RenderTexture renderTexture;
    bool takeScreenshotOnNextFrame;

    private void Awake()
    {
        myCamera.backgroundColor = Color.black;
    }
    private void Start()
    {
        if(takeScreenShots)
            StartCoroutine(GenerateScreenShots());
    }
    [ContextMenu("Manual ScreenShot")]
    public void ManualScreenShot()
    {
        TakeScreenshot(resolution, manualScreenShotObject.name);
    }
    public void DisplayGameObjects(int i)
    {
        foreach(GameObject item in itemsToScreenShot)
            item.SetActive(false);

        itemsToScreenShot[i].SetActive(true);
    }

    public IEnumerator GenerateScreenShots()
    {
        yield return new WaitForSeconds(1f);
        
        int i = 0;
        while (i < itemsToScreenShot.Length)
        {
            DisplayGameObjects(i);
            yield return new WaitForSeconds(0.25f);

            TakeScreenshot(resolution, itemsToScreenShot[i].name);
            i++;

            yield return new WaitForSeconds(0.25f);
        }
        itemsToScreenShot[^1].SetActive(false);
    }
    public IEnumerator onPostRender(string fileName, int width, int height)
    {
        yield return new WaitForEndOfFrame();

        if(takeScreenshotOnNextFrame)
        {
            takeScreenshotOnNextFrame = false;
            renderTexture= myCamera.targetTexture;

            Texture2D renderResult = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

            //used to be 0,0 for bottom left corner, moving it to get center of screen, should have probably just made the screen size 512 by 512
            Rect rect = new ((width-renderTexture.width)/2, (height-renderTexture.height)/2, renderTexture.width, renderTexture.height);
            renderResult.ReadPixels(rect, 0, 0);
            
            if(transparentBackground)
                renderResult = RemoveBackground(renderResult);

            // renderResult = ResizeTexture(renderResult, ImageFilterMode.Average, renderTexture.width, renderTexture.height);

            byte[] byteArray = renderResult.EncodeToPNG();
            //pad to zeros
            // if(fileName.Length<4)
            //     fileName = fileName.PadLeft(3, '0');

            System.IO.File.WriteAllBytes(filepath + fileName +".png", byteArray);
            RenderTexture.ReleaseTemporary(renderTexture);
            myCamera.targetTexture = null;
            // Debug.Log(filepath + fileName + ".png created");
        }
    }
    private Texture2D RemoveBackground(Texture2D renderResult)
    {
        Color[] pixels = renderResult.GetPixels(0, 0, renderTexture.width, renderTexture.height);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == Color.black) {
                pixels[i] = Color.clear;
            }
        }
        renderResult.SetPixels(0, 0, renderTexture.width, renderTexture.height, pixels);
        renderResult.Apply();

        return renderResult;
    }
    public void TakeScreenshot(int2 resolution, string fileName)
    {
        myCamera.targetTexture = RenderTexture.GetTemporary(resolution.x, resolution.y, 32);
        takeScreenshotOnNextFrame = true;
        StartCoroutine(onPostRender(fileName, resolution.x, resolution.y));
    }
    public Texture2D ResizeTexture(Texture2D originalTexture, ImageFilterMode filterMode, int newWidth, int newHeight) 
    {
        //*** Get All the source pixels
        Color[] sourceColor = originalTexture.GetPixels(0);
        Vector2 sourceSize = new (originalTexture.width, originalTexture.height);

        //*** Calculate New Size
        float textureWidth = newWidth;
        float textureHeight = newHeight;

        //*** Make New
        Texture2D newTexture = new ((int)textureWidth, (int)textureHeight, TextureFormat.RGBA32, false);

        //*** Make destination array
        Color[] aColor = new Color[(int)textureWidth * (int)textureHeight];

        Vector2 pixelSize = new (sourceSize.x / textureWidth, sourceSize.y / textureHeight);

        //*** Loop through destination pixels and process
        Vector2 center = new ();
        for (int i = 0; i < aColor.Length; i++) {

            //*** Figure out x&y
            float x = (float)i % textureWidth;
            float y = Mathf.Floor((float)i / textureWidth);

            //*** Calculate Center
            center.x = (x / textureWidth) * sourceSize.x;
            center.y = (y / textureHeight) * sourceSize.y;

            //*** Do Based on mode
            //*** Nearest neighbour (testing)
            if (filterMode == ImageFilterMode.Nearest) {

                //*** Nearest neighbour (testing)
                center.x = Mathf.Round(center.x);
                center.y = Mathf.Round(center.y);

                //*** Calculate source index
                int sourceIndex = (int)((center.y * sourceSize.x) + center.x);

                //*** Copy Pixel
                aColor[i] = sourceColor[sourceIndex];
            }

            //*** Bilinear
            else if (filterMode == ImageFilterMode.Bilinear) {

                //*** Get Ratios
                float ratioX = center.x - Mathf.Floor(center.x);
                float ratioY = center.y - Mathf.Floor(center.y);

                //*** Get Pixel index's
                int indexTL = (int)((Mathf.Floor(center.y) * sourceSize.x) + Mathf.Floor(center.x));
                int indexTR = (int)((Mathf.Floor(center.y) * sourceSize.x) + Mathf.Ceil(center.x));
                int indexBL = (int)((Mathf.Ceil(center.y) * sourceSize.x) + Mathf.Floor(center.x));
                int indexBR = (int)((Mathf.Ceil(center.y) * sourceSize.x) + Mathf.Ceil(center.x));

                //*** Calculate Color
                aColor[i] = Color.Lerp(
                    Color.Lerp(sourceColor[indexTL], sourceColor[indexTR], ratioX),
                    Color.Lerp(sourceColor[indexBL], sourceColor[indexBR], ratioX),
                    ratioY
                );
            }

            //*** Average
            else if (filterMode == ImageFilterMode.Average) {

                //*** Calculate grid around point
                int xFrom = (int)Mathf.Max(Mathf.Floor(center.x - (pixelSize.x * 0.5f)), 0);
                int xTo = (int)Mathf.Min(Mathf.Ceil(center.x + (pixelSize.x * 0.5f)), sourceSize.x);
                int yFrom = (int)Mathf.Max(Mathf.Floor(center.y - (pixelSize.y * 0.5f)), 0);
                int yTo = (int)Mathf.Min(Mathf.Ceil(center.y + (pixelSize.y * 0.5f)), sourceSize.y);

                //*** Loop and accumulate
                Color tempColor = new ();
                float xGridCount = 0;
                for (int iy = yFrom; iy < yTo; iy++) {
                    for (int ix = xFrom; ix < xTo; ix++) {

                        //*** Get Color
                        tempColor += sourceColor[(int)(((float)iy * sourceSize.x) + ix)];

                        //*** Sum
                        xGridCount++;
                    }
                }

                //*** Average Color
                aColor[i] = tempColor / (float)xGridCount;
            }
        }

        //*** Set Pixels
        newTexture.SetPixels(aColor);
        newTexture.Apply();

        //*** Return
        return newTexture;
}
}
}
