using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de debug en pantalla que muestra información del solver IK activo
/// Tal como pide el enunciado:
/// - Nombre del algoritmo activo
/// - Número de iteraciones del último frame
/// - Distancia actual al target
/// </summary>
public class IKDebugPanel : MonoBehaviour
{
    /*[Header("Referencias a los Solvers")]
    public CCDIK ccdSolver;
    public FABRIKIK fabrikSolver;
    
    [Header("UI Elements (opcionales - se crean automáticamente)")]
    public Text algorithmNameText;
    public Text iterationsText;
    public Text distanceText;
    
    [Header("Configuración")]
    public bool autoCreateUI = true;
    public Vector2 panelPosition = new Vector2(10, 10);
    public int fontSize = 16;
    public Color textColor = Color.white;
    
    private Canvas canvas;
    private GameObject panel;
    
    void Start()
    {
        if (autoCreateUI)
        {
            CreateDebugUI();
        }
    }
    
    void Update()
    {
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Actualiza la información del panel con los datos del solver activo
    /// </summary>
    void UpdateDebugInfo()
    {
        // Determinamos qué solver está activo
        string algorithmName = "NINGUNO";
        int iterations = 0;
        float distance = 0f;
        
        if (ccdSolver != null && ccdSolver.isActive)
        {
            algorithmName = ccdSolver.algorithmName;
            iterations = ccdSolver.lastIterationsUsed;
            distance = ccdSolver.lastDistanceToTarget;
        }
        else if (fabrikSolver != null && fabrikSolver.isActive)
        {
            algorithmName = fabrikSolver.algorithmName;
            iterations = fabrikSolver.lastIterationsUsed;
            distance = fabrikSolver.lastDistanceToTarget;
        }
        
        // Actualizamos los textos
        if (algorithmNameText != null)
            algorithmNameText.text = $"Algoritmo: {algorithmName}";
        
        if (iterationsText != null)
            iterationsText.text = $"Iteraciones: {iterations}";
        
        if (distanceText != null)
            distanceText.text = $"Distancia: {distance:F3}m";
    }
    
    /// <summary>
    /// Crea la UI automáticamente si no existe
    /// </summary>
    void CreateDebugUI()
    {
        // Buscar o crear Canvas
        canvas = FindObjectOfType<Canvas>();
        
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DebugCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Crear panel contenedor
        panel = new GameObject("IK_DebugPanel");
        panel.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1); // Esquina superior izquierda
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = panelPosition;
        panelRect.sizeDelta = new Vector2(300, 100);
        
        // Fondo semi-transparente
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.7f);
        
        // Crear textos
        algorithmNameText = CreateText("AlgorithmText", new Vector2(10, -10), "Algoritmo: --");
        iterationsText = CreateText("IterationsText", new Vector2(10, -35), "Iteraciones: 0");
        distanceText = CreateText("DistanceText", new Vector2(10, -60), "Distancia: 0.000m");
    }
    
    /// <summary>
    /// Helper para crear textos UI
    /// </summary>
    Text CreateText(string name, Vector2 position, string initialText)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(panel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(0, 1);
        textRect.pivot = new Vector2(0, 1);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = new Vector2(280, 25);
        
        Text text = textObj.AddComponent<Text>();
        text.text = initialText;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        
        return text;
    }
    
    /// <summary>
    /// Permite cambiar entre solvers en runtime (para testing)
    /// </summary>
    public void SwitchToFABRIK()
    {
        if (ccdSolver != null) ccdSolver.isActive = false;
        if (fabrikSolver != null) fabrikSolver.isActive = true;
    }
    
    public void SwitchToCCD()
    {
        if (fabrikSolver != null) fabrikSolver.isActive = false;
        if (ccdSolver != null) ccdSolver.isActive = true;
    }*/
}
