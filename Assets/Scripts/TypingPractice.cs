using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ByteTyping
{
    /// <summary>
    /// 键盘手打字练习主控制器（复刻 typing.byteworld.cn「键盘手」）。
    /// 空场景挂到任意 GameObject 即可运行：自动构建 Canvas、章节/课程导航、
    /// 逐字符判色正文、进度条、双手手指高亮与程序化键盘。
    /// </summary>
    public class TypingPractice : MonoBehaviour
    {
        private static readonly Color DoneColor = new Color32(0x4A, 0xDE, 0x80, 0xFF); // 已打对：绿
        private static readonly Color CurColor = new Color32(0xFA, 0xCC, 0x15, 0xFF);  // 当前字符：黄
        private static readonly Color ErrColor = new Color32(0xF8, 0x71, 0x71, 0xFF);  // 打错闪烁：红
        private static readonly Color TodoColor = new Color32(0x9C, 0xA3, 0xAF, 0xFF); // 未打：灰

        private CourseData _course;
        private int _chapter, _lesson;
        private string _text = "";
        private int _index;
        private int _errors;
        private bool _completed;
        private bool _isVideo;
        private bool _dirty = true;
        private float _wrongFlashUntil;

        private Text _body;
        private Text _chapterLabel, _lessonLabel, _progressLabel;
        private Image _progressFill;
        private HandOverlay _hands;
        private KeyboardPanel _keyboard;
        private GameObject _completePanel;
        private Text _completeText;

        private void Start()
        {
            _course = CourseLoader.Load();
            if (_course == null) return;
            BuildUI();
            LoadLesson();
        }

        private void Update()
        {
            if (_course == null) return;
            _keyboard.PollPresses();

            if (_completed || _isVideo)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) NextLesson();
                if (_completed && Input.GetKeyDown(KeyCode.R)) LoadLesson();
                return;
            }

            foreach (var c in Input.inputString)
            {
                if (c == '\b') { Backspace(); continue; }
                if (c == '\n' || c == '\r')
                {
                    if (_index < _text.Length && _text[_index] == '\n') OnChar('\n');
                    continue;
                }
                if (c >= ' ' && c != '\u007f') OnChar(c);
            }
            if (_dirty) { RefreshBody(); UpdateProgress(); _dirty = false; }
        }

        // —— 输入逻辑 ——

        private void OnChar(char c)
        {
            if (_index >= _text.Length) return;
            if (c == _text[_index])
            {
                _index++;
                _dirty = true;
                UpdateTarget();
                if (_index >= _text.Length) Complete();
            }
            else
            {
                _errors++;
                _wrongFlashUntil = Time.time + 0.3f;
                _dirty = true;
            }
        }

        private void Backspace()
        {
            if (_index <= 0) return;
            _index--;
            _dirty = true;
            UpdateTarget();
        }

        private void Complete()
        {
            _completed = true;
            UpdateTarget();
            _completePanel.SetActive(true);
            _completeText.text = $"本课完成 · 错误 {_errors} 次\n\nEnter = 下一课      R = 重打本课";
        }

        // —— 高亮联动 ——

        private void UpdateTarget()
        {
            if (_isVideo || _completed || _index >= _text.Length)
            {
                _keyboard.SetTarget("", "");
                _hands.SetHighlight("", "", "");
                return;
            }
            if (FingerMap.Resolve(_text[_index], out var t))
            {
                _keyboard.SetTarget(t.keyCode, t.shiftKeyCode);
                _hands.SetHighlight(t.leftLayer, t.rightLayer, t.shiftKeyCode);
            }
            else
            {
                _keyboard.SetTarget("", "");
                _hands.SetHighlight("", "", "");
            }
        }

        // —— 正文渲染（逐字符 Rich Text 判色）——

        private static string EscStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private void RefreshBody()
        {
            var sb = new System.Text.StringBuilder(_text.Length * 24);
            for (int i = 0; i < _text.Length; i++)
            {
                Color col = i < _index ? DoneColor
                    : i == _index ? (Time.time < _wrongFlashUntil ? ErrColor : CurColor)
                    : TodoColor;
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(col)).Append('>')
                  .Append(EscStr(_text[i].ToString())).Append("</color>");
            }
            _body.text = sb.ToString();
        }

        private void UpdateProgress()
        {
            _progressFill.fillAmount = _text.Length == 0 ? 0f : (float)_index / _text.Length;
            _progressLabel.text = $"{_index}/{_text.Length}   错误 {_errors}";
        }

        // —— 课程装载 ——

        private void LoadLesson()
        {
            var ch = _course.chapters[_chapter];
            var ls = ch.lessons[_lesson];
            _text = (ls.text ?? "").Replace("\r", "").Replace("\n", "");
            _index = 0;
            _errors = 0;
            _completed = false;
            _wrongFlashUntil = 0f;
            _completePanel.SetActive(false);
            _chapterLabel.text = ch.name;
            _lessonLabel.text = ls.title;

            if (ls.type == "video")
            {
                _isVideo = true;
                _body.text = $"<color=#FACC15>{EscStr(ls.title)}</color>\n\n{EscStr(ls.text)}\n\n" +
                             "<color=#9CA3AF>本课为教学视频课，请在网页版观看 · 按 Enter 进入下一课</color>";
                UpdateTarget();
                _progressFill.fillAmount = 1f;
                _progressLabel.text = "--";
                _dirty = false;
                return;
            }

            _isVideo = false;
            UpdateTarget();
            RefreshBody();
            UpdateProgress();
            _dirty = false;
        }

        private void NextLesson()
        {
            if (_lesson < _course.chapters[_chapter].lessons.Count - 1) _lesson++;
            else if (_chapter < _course.chapters.Count - 1) { _chapter++; _lesson = 0; }
            else return; // 已是最后一课
            LoadLesson();
        }

        private void PrevLesson()
        {
            if (_lesson > 0) _lesson--;
            else if (_chapter > 0) { _chapter--; _lesson = _course.chapters[_chapter].lessons.Count - 1; }
            else return;
            LoadLesson();
        }

        private void NextChapter()
        {
            if (_chapter >= _course.chapters.Count - 1) return;
            _chapter++;
            _lesson = 0;
            LoadLesson();
        }

        private void PrevChapter()
        {
            if (_chapter <= 0) return;
            _chapter--;
            _lesson = 0;
            LoadLesson();
        }

        // —— 程序化 UI ——

        private void BuildUI()
        {
            var canvasGo = new GameObject("TypingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var root = canvasGo.transform;

            // 顶部导航：章节（左） / 课程（右）
            UI.Btn(root, "<", new Vector2(56, 44), new Vector2(-880, 328), PrevChapter);
            _chapterLabel = UI.Txt(root, "chapterLabel", "", 24, new Color(0.85f, 0.85f, 0.9f),
                new Vector2(340, 44), new Vector2(-670, 328));
            UI.Btn(root, ">", new Vector2(56, 44), new Vector2(-460, 328), NextChapter);
            UI.Btn(root, "<", new Vector2(56, 44), new Vector2(400, 328), PrevLesson);
            _lessonLabel = UI.Txt(root, "lessonLabel", "", 24, new Color(0.85f, 0.85f, 0.9f),
                new Vector2(460, 44), new Vector2(700, 328));
            UI.Btn(root, ">", new Vector2(56, 44), new Vector2(930, 328), NextLesson);

            // 进度条 + 统计
            var pbg = UI.Img(root, "progressBg", new Color(1f, 1f, 1f, 0.08f),
                new Vector2(1000, 16), new Vector2(-140, 281));
            var fill = UI.Stretch(pbg.transform, "fill").gameObject.AddComponent<Image>();
            fill.color = new Color32(0xDB, 0x73, 0xEB, 0xFF);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill = fill;
            _progressLabel = UI.Txt(root, "progressLabel", "", 20, new Color(0.7f, 0.7f, 0.78f),
                new Vector2(300, 44), new Vector2(500, 281));

            // 练习正文
            _body = UI.Txt(root, "body", "", 36, TodoColor, new Vector2(1800, 90), new Vector2(0, 215));
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 双手高亮 + 键盘
            _hands = HandOverlay.Create(root, 170, new Vector2(0, 88));
            _keyboard = KeyboardPanel.Create(root, new Vector2(0, -127));

            // 完成遮罩
            _completePanel = UI.Stretch(root, "CompletePanel").gameObject;
            _completePanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            _completeText = UI.Txt(_completePanel.transform, "text", "", 40, CurColor,
                new Vector2(1200, 300), Vector2.zero);
            _completePanel.SetActive(false);
        }
    }
}
