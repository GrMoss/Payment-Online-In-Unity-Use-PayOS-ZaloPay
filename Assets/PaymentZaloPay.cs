using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class PaymentZaloPay : MonoBehaviour
{
    public UniWebView webView;

    // Sandbox credentials
    public string appId = "554";
    public string key1 = "8NdU5pG5R2spGHGhyO99HN1OhD8IQJBn";
    public string key2 = "uUfsWgfLkRLzq6W2uNXTCxrfxs51auny";

    public long amount = 10000;
    public string appUser = "dinhnt24_001";
    public string description = "Thanh toan trong Unity";

    private string appTransId;

    public TMP_Text txtResult;

    public void CreatePaymentZaloPay()
    {
        txtResult.text = "Đang thực hiện thanh toán bằng phương thức Zalopay (Link)";
        StartCoroutine(CreateZaloPay());
    }

    IEnumerator CreateZaloPay()
    {
        // yymmdd phải theo giờ VN (GMT+7)
        var nowVN = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
        string yymmdd = nowVN.ToString("yyMMdd");
        appTransId = $"{yymmdd}_{UnityEngine.Random.Range(100000, 999999)}";
        long appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // embed_data.redirecturl theo docs (không có dấu gạch dưới trong key)
        string embedData = "{\"redirecturl\":\"https://dinhnt.com/return\"}";
        string item = "[]";

        // HMAC input đúng chuẩn: app_id|app_trans_id|app_user|amount|app_time|embed_data|item
        string macData = $"{appId}|{appTransId}|{appUser}|{amount}|{appTime}|{embedData}|{item}";
        string mac = HmacSHA256(key1, macData);

        // Dùng x-www-form-urlencoded (không dùng WWWForm để tránh multipart)
        var form = new Dictionary<string, string> {
            { "app_id",        appId },
            { "app_user",      appUser },
            { "app_trans_id",  appTransId },
            { "app_time",      appTime.ToString() },
            { "amount",        amount.ToString() },
            { "embed_data",    embedData },
            { "item",          item },
            { "description",   description },
            { "bank_code",     "" },
            { "mac",           mac }
        };

        using UnityWebRequest req = UnityWebRequest.Post("https://sb-openapi.zalopay.vn/v2/create", form);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("CreateOrder Response: " + req.downloadHandler.text);

            var json = JsonUtility.FromJson<CreateOrderResponse>(EnsureOrderUrlField(req.downloadHandler.text));
            if (json.return_code == 1)
            {
                // Nếu có order_url thì mở cổng thanh toán
                if (!string.IsNullOrEmpty(json.order_url))
                {
                    OpenWebView(json.order_url);
                }
            }
            else
            {
                Debug.LogError($"Create order failed: {json.return_message} ({json.return_code})");
            }
        }
        else
        {
            Debug.LogError("Request error: " + req.error);
        }
    }

    void OpenWebView(string url)
    {
        var go = new GameObject("ZaloPayWebView");
        webView = go.AddComponent<UniWebView>();
        webView.Frame = new Rect(0, 0, Screen.width, Screen.height);

        webView.OnPageStarted += OnPageStarted;
        webView.OnPageFinished += (view, code, u) => Debug.Log("Loaded: " + u);
        webView.Load(url);
        webView.Show();
    }

    void OnPageStarted(UniWebView view, string url)
    {
        Debug.Log("Redirect: " + url);

        if (url.Contains("dinhnt.com/return"))
        {
            webView.Hide();
            txtResult.text = "Giao dịch thành công";
        }
    }


    string HmacSHA256(string key, string data)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            byte[] hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    [Serializable]
    public class CreateOrderResponse
    {
        public int return_code;
        public string return_message;
        public string sub_return_code;
        public string sub_return_message;
        public string order_url;       // dùng mở cổng/QR page
        public string zp_trans_token;  // dùng cho SDK payOrder
        public string order_token;
        public string qr_code;         // VietQR payload (NAPAS) để tự render QR
    }

    // Bổ sung field thiếu để JsonUtility không null khi server không trả order_url
    string EnsureOrderUrlField(string raw)
    {
        if (!raw.Contains("\"order_url\""))
        {
            return raw.TrimEnd('}') + ",\"order_url\":\"\"}";
        }
        return raw;
    }
}
