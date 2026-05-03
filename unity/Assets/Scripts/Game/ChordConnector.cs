using UnityEngine;

// 同時押しノーツ間をつなぐ水平バー。参照ノーツが消えたら自身も破棄する。
public class ChordConnector : MonoBehaviour
{
    NoteController _ref;
    float _centerX;

    public void Init(NoteController referenceNote, float xMin, float xMax)
    {
        _ref    = referenceNote;
        _centerX = (xMin + xMax) * 0.5f;
        transform.localScale = new Vector3(xMax - xMin, 0.03f, 0.06f);

        var shader = Shader.Find("Unlit/Color")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);
        HighwayBuilder.ApplyColor(mat, new Color(1f, 1f, 1f, 0.75f));
        GetComponent<Renderer>().material = mat;

        UpdatePosition();
    }

    void Update()
    {
        if (_ref == null || !_ref.gameObject.activeSelf || _ref.IsMissed)
        {
            Destroy(gameObject);
            return;
        }
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (_ref == null) return;
        transform.position = new Vector3(_centerX, 0.04f, _ref.transform.position.z);
    }
}
