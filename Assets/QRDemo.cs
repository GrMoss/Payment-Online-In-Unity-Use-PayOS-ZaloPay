using UnityEngine;
using UnityEngine.UI;
using ZXing;   // từ ZXing.Net
using ZXing.QrCode;

//Lấy thư viện trong folder _LibShowQRCode bỏ vào Assets/Plugins
public class QRDemo : MonoBehaviour
{
    public RawImage qrImage;

    public void GenQR(string vietQRPayload)
    {
        if (qrImage != null)
        {
            qrImage.texture = GenerateQR(vietQRPayload, 256, 256);
        }
    }

    private Texture2D GenerateQR(string textForEncoding, int width, int height)
    {
        var barcodeWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = height,
                Width = width,
                Margin = 1
            }
        };

        var pixelData = barcodeWriter.Write(textForEncoding);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // Convert byte array to Color32 array
        Color32[] colors = new Color32[pixelData.Pixels.Length / 4];
        for (int i = 0; i < colors.Length; i++)
        {
            int index = i * 4;
            colors[i] = new Color32(
                pixelData.Pixels[index],     // R
                pixelData.Pixels[index + 1], // G
                pixelData.Pixels[index + 2], // B
                pixelData.Pixels[index + 3]  // A
            );
        }

        tex.SetPixels32(colors);
        tex.Apply();

        return tex;
    }
}
