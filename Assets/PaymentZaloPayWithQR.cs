using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class PaymentZaloPayWithQR : MonoBehaviour
{
    public string appId = "554";
    public string key1 = "8NdU5pG5R2spGHGhyO99HN1OhD8IQJBn";
    public string key2 = "uUfsWgfLkRLzq6W2uNXTCxrfxs51auny";
    public long amount = 10000;
    public string appUser = "dinhnt24_001";
    public string description = "Thanh toan trong Unity";

    private string appTransId;

    public QRDemo qRDemo;

    public TMP_Text txtResult;

    public void Payment()
    {
        txtResult.text = "Kết quả giao dịch";
        StartCoroutine(CreateOrder());
    }

    IEnumerator CreateOrder()
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
                // Nếu có order_url thì show QRCode
                if (!string.IsNullOrEmpty(json.order_url))
                {
                    qRDemo.GenQR(json.order_url);
                }
                // Sau khi tạo đơn, thử gọi query trạng thái
                StartCoroutine(PollingQueryOrder());
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

    IEnumerator PollingQueryOrder(float interval = 3f, int maxRetry = 100)
    {
        int count = 0;
        while (count < maxRetry)
        {
            yield return QueryOrder();   // gọi check
            yield return new WaitForSeconds(interval);

            // Nếu đã có kết quả thành công/thất bại thì thoát luôn
            if (lastQueryStatus == QueryStatus.Success || lastQueryStatus == QueryStatus.Failed)
            {
                Debug.Log("Stop polling vì đã có kết quả cuối: " + lastQueryStatus);
                txtResult.text = "Kết quả giao dịch: " + lastQueryStatus;
                break;
            }

            count++;
        }
        if (count >= maxRetry)
        {
            Debug.Log("Polling hết thời gian mà chưa có kết quả thanh toán.");
        }
    }

    // Enum trạng thái đơn
    public enum QueryStatus
    {
        Unknown,
        Processing,
        Success,
        Failed
    }
    private QueryStatus lastQueryStatus = QueryStatus.Unknown;

    IEnumerator QueryOrder()
    {
        string data = $"{appId}|{appTransId}|{key1}";
        string mac = HmacSHA256(key1, data);

        var form = new Dictionary<string, string> {
        { "app_id",       appId },
        { "app_trans_id", appTransId },
        { "mac",          mac }
    };

        using UnityWebRequest req = UnityWebRequest.Post("https://sb-openapi.zalopay.vn/v2/query", form);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("QueryOrder Response: " + req.downloadHandler.text);

            var json = JsonUtility.FromJson<QueryOrderResponse>(req.downloadHandler.text);
            if (json.return_code == 1)
            {
                lastQueryStatus = QueryStatus.Success;
                Debug.Log("Thanh toán thành công");
            }
            else if (json.return_code == 2)
            {
                lastQueryStatus = QueryStatus.Failed;
                Debug.Log("Thanh toán thất bại");
            }
            else
            {
                lastQueryStatus = QueryStatus.Processing;
                Debug.Log("Đơn hàng chưa thanh toán hoặc giao dịch đang xử lý");
            }
        }
        else
        {
            Debug.Log("Request error: " + req.error);
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

    [Serializable]
    public class QueryOrderResponse
    {
        public int return_code;
        public string return_message;
        public string sub_return_code;
        public string sub_return_message;
        public string is_processing;
        public string amount;
        public string zp_trans_id;
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
