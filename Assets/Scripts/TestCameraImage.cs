using System;
using System.Collections;
using System.Web;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Crosstales.RTVoice;

/// <summary>
/// This component tests getting the latest camera image
/// and converting it to RGBA format. If successful,
/// it displays the image on the screen as a RawImage
/// and also displays information about the image.
///
/// This is useful for computer vision applications where
/// you need to access the raw pixels from camera image
/// on the CPU.
///
/// This is different from the ARCameraBackground component, which
/// efficiently displays the camera image on the screen. If you
/// just want to blit the camera texture to the screen, use
/// the ARCameraBackground, or use Graphics.Blit to create
/// a GPU-friendly RenderTexture.
///
/// In this example, we get the camera image data on the CPU,
/// convert it to an RGBA format, then display it on the screen
/// as a RawImage texture to demonstrate it is working.
/// This is done as an example; do not use this technique simply
/// to render the camera image on screen.
/// </summary>
public class TestCameraImage : MonoBehaviour
{
    private class ObjRecognitionResult
    {
        public class RecognitionResultItem
        {
            public float score { get; set; }
            public string root { get; set; }
            public string keyword { get; set; }
        }
        public long log_id { get; set; }
        public int result_num { get; set; }
        public IList<RecognitionResultItem> result { get; set; }
    }

    [SerializeField]
    [Tooltip("The ARCameraManager which will produce frame events.")]
    ARCameraManager m_CameraManager;

     /// <summary>
    /// Get or set the <c>ARCameraManager</c>.
    /// </summary>
    public ARCameraManager cameraManager
    {
        get { return m_CameraManager; }
        set { m_CameraManager = value; }
    }

    [SerializeField]
    RawImage m_RawImage;

    public Button objectRecognitionButton;
    private bool m_DoObjectRecognition = false;
    private Dictionary<string, string> m_ChineseToEnglishMap = new Dictionary<string, string>();
    private string m_GoogleTranslationUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=zh&tl=en&dt=t&q=";

    /// <summary>
    /// The UI RawImage used to display the image on screen.
    /// </summary>
    public RawImage rawImage
    {
        get { return m_RawImage; }
        set { m_RawImage = value; }
    }

    [SerializeField]
    Text m_ImageInfo;

    /// <summary>
    /// The UI Text used to display information about the image on screen.
    /// </summary>
    public Text imageInfo
    {
        get { return m_ImageInfo; }
        set { m_ImageInfo = value; }
    }

    void Start()
    {
        Button btn = objectRecognitionButton.GetComponent<Button>();
        btn.onClick.AddListener(OnRecognitionClicked);
        InitializeChineseToEnglishMap();
    }

    private void InitializeChineseToEnglishMap()
    {
        m_ChineseToEnglishMap.Add("商品-电脑办公鼠标", "mouse");
        m_ChineseToEnglishMap.Add("动物-爬行动物铜斑蛇", "copperhead snake");
        m_ChineseToEnglishMap.Add("商品-首饰吊坠", "pendant");
        m_ChineseToEnglishMap.Add("商品-电脑办公光电鼠", "photoelectric mouse");
        m_ChineseToEnglishMap.Add("商品-数码产品台式电脑", "desktop pc");
        m_ChineseToEnglishMap.Add("商品-电脑办公笔记本电脑", "laptop");
        m_ChineseToEnglishMap.Add("商品-电脑办公笔记本", "laptop");
        m_ChineseToEnglishMap.Add("商品-电脑办公电脑", "computer");
        m_ChineseToEnglishMap.Add("商品-电脑办公鼠标垫", "mouse pad");
        m_ChineseToEnglishMap.Add("商品-电脑办公键盘", "keyboard");
        m_ChineseToEnglishMap.Add("商品-电脑办公订书机", "stapler");
        m_ChineseToEnglishMap.Add("建筑-室内室内一角", "corner of room");
        m_ChineseToEnglishMap.Add("建筑-居家室内电视背景墙", "TV backdrop");
        m_ChineseToEnglishMap.Add("建筑-商店商场商场", "shopping mall");
        m_ChineseToEnglishMap.Add("商品-电脑办公显示器屏幕", "monitor");
        m_ChineseToEnglishMap.Add("商品-家用电器热水器", "water heater");
        m_ChineseToEnglishMap.Add("商品-户外用品保温杯", "thermos cup");
        m_ChineseToEnglishMap.Add("商品-原材料缠绕膜", "stretch film");
        m_ChineseToEnglishMap.Add("商品-容器杯子", "cup");
        m_ChineseToEnglishMap.Add("商品-机器设备饮水机", "water dispenser");
        m_ChineseToEnglishMap.Add("商品-保健器械台灯", "table lamp");
        m_ChineseToEnglishMap.Add("商品-电脑办公电脑外设", "computer peripherals");
        m_ChineseToEnglishMap.Add("商品-电脑办公路由器", "router");
        m_ChineseToEnglishMap.Add("商品-箱包手挎包", "Shoulder bag");
        m_ChineseToEnglishMap.Add("商品-箱包行李袋", "Luggage bag");
        m_ChineseToEnglishMap.Add("商品-箱包双肩包", "backpack");
        m_ChineseToEnglishMap.Add("商品-箱包公文包", "briefcase");
        m_ChineseToEnglishMap.Add("商品-箱包驮包", "Carry bag");
        m_ChineseToEnglishMap.Add("商品-穿戴钱带", "Money belt");
        m_ChineseToEnglishMap.Add("商品-箱包运动包", "sports bag");
    }

    void OnEnable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }


    void OnRecognitionClicked()
    {
        m_DoObjectRecognition = true;
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (m_DoObjectRecognition)
        {
            m_DoObjectRecognition = false;
            // Attempt to get the latest camera image. If this method succeeds,
            // it acquires a native resource that must be disposed (see below).
            XRCameraImage image;
            if (!cameraManager.TryGetLatestImage(out image))
            {
                return;
            }

            Console.WriteLine(string.Format(
                "Image info:\n\twidth: {0}\n\theight: {1}\n\tplaneCount: {2}\n\ttimestamp: {3}\n\tformat: {4}",
                image.width, image.height, image.planeCount, image.timestamp, image.format));

            // Once we have a valid XRCameraImage, we can access the individual image "planes"
            // (the separate channels in the image). XRCameraImage.GetPlane provides
            // low-overhead access to this data. This could then be passed to a
            // computer vision algorithm. Here, we will convert the camera image
            // to an RGBA texture and draw it on the screen.

            // Choose an RGBA format.
            // See XRCameraImage.FormatSupported for a complete list of supported formats.
            var format = TextureFormat.RGB24;
            var targetWidth = image.width / 4;
            var targetHeight = image.height / 4;

            if (m_Texture == null || m_Texture.width != targetWidth || m_Texture.height != targetHeight)
            {
                m_Texture = new Texture2D(targetWidth, targetHeight, format, false);
            }

            // Convert the image to format, flipping the image across the Y axis.
            // We can also get a sub rectangle, but we'll get the full image here.
            var conversionParams = new XRCameraImageConversionParams(image, format, CameraImageTransformation.None);
            conversionParams.outputDimensions = new Vector2Int(targetWidth, targetHeight);

            // Texture2D allows us write directly to the raw texture data
            // This allows us to do the conversion in-place without making any copies.
            var rawTextureData = m_Texture.GetRawTextureData<byte>();
            try
            {
                image.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
            }
            finally
            {
                // We must dispose of the XRCameraImage after we're finished
                // with it to avoid leaking native resources.
                image.Dispose();
            }

            // Apply the updated texture data to our texture
            m_Texture.Apply();

            // Set the RawImage's texture so we can visualize it.
            m_RawImage.texture = m_Texture;

            StartCoroutine(HandleObjectRecognitionBaidu());
        }
    }

    string m_TranslatedResult = "";
    IEnumerator translateResultWithGoogleApi(string source)
    {
        string url = m_GoogleTranslationUrl + UnityWebRequest.EscapeURL(source);
        UnityWebRequest translationRequest = UnityWebRequest.Get(url);
        yield return translationRequest.SendWebRequest();
        List<object> resultList = JsonConvert.DeserializeObject<List<object>>(translationRequest.downloadHandler.text);
        if (resultList.Count > 0)
        {
            JArray resultArray = (JArray)(resultList[0]);
            if (resultArray != null && resultArray.Count > 0)
            {
                JArray finalResult = (JArray)(resultArray.First);
                if (finalResult != null && finalResult.Count > 0)
                {
                    m_TranslatedResult = finalResult.First.ToString();
                }
            }
        }
    }

    IEnumerator HandleObjectRecognitionBaidu()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get("http://10.128.58.107:5000/bceimagetagoptions"))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            if (!webRequest.isNetworkError)
            {
                m_ImageInfo.text = webRequest.downloadHandler.text;
                byte[] bytes = m_Texture.EncodeToPNG();
                string base64String = Convert.ToBase64String(bytes);
                //string encodedBase64String = Uri.EscapeUriString(base64String);
                List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
                formData.Add(new MultipartFormDataSection("image", base64String));
                UnityWebRequest bceRequest = UnityWebRequest.Post(webRequest.downloadHandler.text, formData);
                yield return bceRequest.SendWebRequest();
                if (!bceRequest.isNetworkError && !bceRequest.isHttpError)
                {
                    m_ImageInfo.text = bceRequest.downloadHandler.text;
                    ObjRecognitionResult resultObj = JsonConvert.DeserializeObject<ObjRecognitionResult>(m_ImageInfo.text);
                    if (resultObj.result.Count > 0)
                    {
                        ObjRecognitionResult.RecognitionResultItem firstItem = resultObj.result[0];
                        string tag = firstItem.root + firstItem.keyword;
                        if (m_ChineseToEnglishMap.ContainsKey(tag))
                        {
                            Speaker.Speak("result is: " + m_ChineseToEnglishMap[tag] + ", score is: " + firstItem.score.ToString());
                        }
                        else
                        {
                            yield return StartCoroutine(translateResultWithGoogleApi(firstItem.keyword));
                            Console.WriteLine("m_ChineseToEnglishMap.Add(\"" + tag + "\", \"" + m_TranslatedResult + "\");");
                            m_ChineseToEnglishMap.Add(tag, m_TranslatedResult);
                            Speaker.Speak("result is: " + m_TranslatedResult + ", score is: " + firstItem.score.ToString());
                        }
                        foreach (var item in resultObj.result)
                        {
                            tag = item.root + item.keyword;
                            if (!m_ChineseToEnglishMap.ContainsKey(tag))
                            {
                                yield return StartCoroutine(translateResultWithGoogleApi(item.keyword));
                                Console.WriteLine("m_ChineseToEnglishMap.Add(\"" + tag + "\", \"" + m_TranslatedResult + "\");");
                                m_ChineseToEnglishMap.Add(tag, m_TranslatedResult);
                            }
                        }
                    }
                }
                else
                {
                    m_ImageInfo.text = "get from bce error";
                }
            }
            else
            {
                m_ImageInfo.text = "get from token server error";
            }
        }
    }

    Texture2D m_Texture;
}
