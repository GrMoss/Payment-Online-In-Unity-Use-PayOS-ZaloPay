using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PaymentZaloPay : MonoBehaviour
{
    public UniWebView webView;
    public string appId = "554"; // từ ZaloPay
    public string key1  = "8NdU5pG5R2spGHGhyO99HN1OhD8IQJBn";
    public string key2  = "uUfsWgfLkRLzq6W2uNXTCxrfxs51auny";

    public long amount = 10000;
    public string appUser = "dinhnt24_001";
    public string description = "Thanh toan trong Unity";
    private string apptransid;

    public void Payment() {
        StartCoroutine(CreateOrder());
    }

    IEnumerator CreateOrder() {
        string yymmdd = DateTime.Now.ToString("yyMMdd");
        apptransid = $"{yymmdd}_{UnityEngine.Random.Range(100000,999999)}";
        long apptime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string embeddata = "{\"redirecturl\":\"https://dinhnt.com/return\"}";
        string item = "[]";

        // MAC = appid|apptransid|appuser|amount|apptime|embeddata|item
        string macData = $"{appId}|{apptransid}|{appUser}|{amount}|{apptime}|{embeddata}|{item}";
        string mac = HmacSHA256(key1, macData);

        WWWForm form = new WWWForm();
        form.AddField("appid", appId);
        form.AddField("apptransid", apptransid);
        form.AddField("appuser", appUser);
        form.AddField("apptime", apptime.ToString());
        form.AddField("amount", amount.ToString());
        form.AddField("embeddata", embeddata);
        form.AddField("item", item);
        form.AddField("description", description);
        form.AddField("mac", mac);

        using UnityWebRequest req = UnityWebRequest.Post("https://sandbox.zalopay.com.vn/v001/tpe/createorder", form);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success) {
            Debug.Log("Response: " + req.downloadHandler.text);

            var json = JsonUtility.FromJson<CreateOrderResponse>(FixJson(req.downloadHandler.text));
            if (json.returncode == 1 && !string.IsNullOrEmpty(json.orderurl)) {
                OpenWebView(json.orderurl);
            } else {
                Debug.LogError("Create order failed: " + json.returnmessage);
            }
        } else {
            Debug.LogError("Request error: " + req.error);
        }
    }

    void OpenWebView(string url) {
        var go = new GameObject("ZaloPayWebView");
        webView = go.AddComponent<UniWebView>();
        webView.Frame = new Rect(0, 0, Screen.width, Screen.height);

        webView.OnPageStarted += OnPageStarted;
        webView.OnPageFinished += (view, code, u) => Debug.Log("Loaded: " + u);
        webView.Load(url);
        webView.Show();
    }

    void OnPageStarted(UniWebView view, string url) {
        Debug.Log("Redirect: " + url);

        if (url.Contains("dinhnt.com/return")) {
            // User đã thanh toán xong, giờ kiểm tra trạng thái
            webView.Hide();
            StartCoroutine(QueryStatus());
        }
    }

    IEnumerator QueryStatus() {
        string mac = HmacSHA256(key1, $"{appId}|{apptransid}|{key1}");

        WWWForm form = new WWWForm();
        form.AddField("appid", appId);
        form.AddField("apptransid", apptransid);
        form.AddField("mac", mac);

        using UnityWebRequest req = UnityWebRequest.Post("https://sandbox.zalopay.com.vn/v001/tpe/getstatusbyapptransid", form);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success) {
            Debug.Log("Status response: " + req.downloadHandler.text);
        } else {
            Debug.LogError("Status error: " + req.error);
        }
    }

    string HmacSHA256(string key, string data) {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        using (var hmac = new HMACSHA256(keyBytes)) {
            byte[] hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    [Serializable]
    public class CreateOrderResponse {
        public int returncode;
        public string returnmessage;
        public string orderurl;
        public string zptranstoken;
    }

    // Fix Json vì JsonUtility của Unity không thích object thiếu field
    string FixJson(string raw) {
        if (!raw.Contains("\"orderurl\"")) {
            raw = raw.TrimEnd('}') + ",\"orderurl\":\"\"}";
        }
        return raw;
    }
    
}