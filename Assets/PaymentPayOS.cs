using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PaymentPayOS : MonoBehaviour
{
    public UniWebView webView;
    public string xClientId = "tự lấy đi, ai mà cho"; //lấy trên PayOS
    public string xApiKey = "tự lấy đi, ai mà cho"; //lấy trên PayOS
    public string checksumKey = "tự lấy đi, ai mà cho"; //lấy trên PayOS
    public string partnerCodeOptional;

    public int amount = 5000; //thay đổi số tiền cần thanh toán
    public string description = "UNITY01"; //tự tạo mã đơn nếu muốn
    public string cancelUrl = "https://dinhnt.com/cancel"; //có thể thay bằng link khác
    public string returnUrl = "https://dinhnt.com/return"; //có thể thay bằng link khác

    public void CreateLinkPayment()
    {
        StartCoroutine(CreatePaymentLink());
    }

    IEnumerator CreatePaymentLink()
    {
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // đảm bảo duy nhất
        int expiredAt = (int)DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        //Tạo data để ký theo đúng thứ tự alphabet
        string toSign =
            $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

        //HMAC_SHA256 với checksumKey
        string signature = HmacSha256Hex(toSign, checksumKey);

        //Tạo payload
        var req = new CreatePaymentRequest
        {
            orderCode = orderCode,
            amount = amount,
            description = description,
            cancelUrl = cancelUrl,
            returnUrl = returnUrl,
            expiredAt = expiredAt,
            signature = signature,
            items = null
        };

        string json = JsonUtility.ToJson(req);

        //Gọi API
        using (var www = new UnityWebRequest("https://api-merchant.payos.vn/v2/payment-requests", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("x-client-id", xClientId);
            www.SetRequestHeader("x-api-key", xApiKey);
            if (!string.IsNullOrEmpty(partnerCodeOptional))
                www.SetRequestHeader("x-partner-code", partnerCodeOptional);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"PayOS error: {www.responseCode} - {www.error}\n{www.downloadHandler.text}");
                yield break;
            }

            //Parse checkoutUrl từ response
            var jsonText = www.downloadHandler.text;
            string checkoutUrl = ExtractCheckoutUrl(jsonText);
            if (!string.IsNullOrEmpty(checkoutUrl))
            {
                OpenLinkPayment(checkoutUrl);
            }
            else
            {
                Debug.LogWarning("Không tìm thấy checkoutUrl trong phản hồi: " + jsonText);
            }
        }
    }

    static string HmacSha256Hex(string message, string secret)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // Tách nhanh checkoutUrl
    static string ExtractCheckoutUrl(string json)
    {
        const string key = "\"checkoutUrl\":\"";
        int i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i += key.Length;
        int j = json.IndexOf("\"", i, StringComparison.Ordinal);
        if (j < 0) return null;
        return json.Substring(i, j - i).Replace("\\/", "/");
    }

    private void OpenLinkPayment(string link)
    {
        webView = gameObject.AddComponent<UniWebView>();
        webView.Frame = new Rect(0, 0, Screen.width, Screen.height);
        webView.OnPageStarted += (view, url) =>
        {
            Debug.Log("Đang mở: " + url);

            // Nếu là returnUrl (thanh toán thành công)
            if (url.StartsWith(returnUrl))
            {
                Debug.Log("Thanh toán thành công!");
                webView.Hide();
                Destroy(webView);

                // Gọi API cập nhật
            }

            // Nếu là cancelUrl (hủy thanh toán)
            if (url.StartsWith(cancelUrl))
            {
                Debug.Log("Thanh toán bị hủy!");
                webView.Hide();
                Destroy(webView);
                //xử lý hủy link thanh toán
            }
        };

        webView.Load(link);
        webView.Show();
    }
}

[Serializable]
public class PayOSItem
{
    public string name;
    public int quantity;
    public int price;
}

[Serializable]
public class CreatePaymentRequest
{
    public long orderCode;
    public int amount;
    public string description;
    public string cancelUrl;
    public string returnUrl;
    public int expiredAt;
    public string signature;
    public List<PayOSItem> items;
}
