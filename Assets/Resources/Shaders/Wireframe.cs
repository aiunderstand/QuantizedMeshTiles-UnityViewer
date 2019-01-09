using UnityEngine;
using System.Collections;

public class Wireframe : MonoBehaviour
{

    Mesh myMesh;
    Mesh newMesh, newMeshBackground;
    Vector3[] myVertices;
    int[] myTriangles;
    public Shader myShader;
    public Color myColor = Color.black;
    public Color myColorBackground = Color.cyan;
    Material myMaterial, myMaterialBackground;
    public float lineSize = .05f;
    float lineSizeBackground;
    public bool drawBackground = false;
    Vector3 startScale;
    Quaternion startRotation;
    Transform startParent;

    void Awake()
    {
        //Preloaded scale/rotation strangely effects mesh generation so we store them and restore them at the end of Start
        //Store
        startParent = transform.parent;
        transform.parent = null;
        //Reset
        startScale = transform.localScale;
        startRotation = transform.localRotation;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        myShader = Shader.Find("Custom/StandardShaderWithVertexColor");
    }

    void Start()
    {
        MeshFilter myMeshFilter = GetComponent<MeshFilter>();
        SkinnedMeshRenderer mySkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (myMeshFilter)
        {
            myMesh = myMeshFilter.mesh;
        }
        else if (mySkinnedMeshRenderer)
        {
            myMesh = mySkinnedMeshRenderer.sharedMesh;
        }
        else
        {
            Debug.Log("No mesh found.");
        }
        myVertices = myMesh.vertices;
        myTriangles = myMesh.triangles;

        newMesh = new Mesh();
        newMeshBackground = new Mesh();
        myMaterial = new Material(myShader);
        myMaterialBackground = new Material(myShader);
        myMaterialBackground.color = myColorBackground;

        lineSizeBackground = lineSize * 2f;

        verticesOne = new Vector3[myTriangles.Length * 4];
        verticesTwo = new Vector3[myTriangles.Length * 4];
        trianglesOne = new int[myTriangles.Length * 6];
        trianglesTwo = new int[myTriangles.Length * 6];

        for (int i = 0; i < myTriangles.Length - 3; i += 3)
        {
            Vector3 s = Vector3.Scale(transform.localScale, myVertices[myTriangles[i]]);
            Vector3 e = Vector3.Scale(transform.localScale, myVertices[myTriangles[i + 1]]);
            AddLine(newMesh, newMeshBackground, MakeQuad(s, e, lineSize, lineSizeBackground), false);
            s = Vector3.Scale(transform.localScale, myVertices[myTriangles[i + 1]]);
            e = Vector3.Scale(transform.localScale, myVertices[myTriangles[i + 2]]);
            AddLine(newMesh, newMeshBackground, MakeQuad(s, e, lineSize, lineSizeBackground), false);
            s = Vector3.Scale(transform.localScale, myVertices[myTriangles[i + 2]]);
            e = Vector3.Scale(transform.localScale, myVertices[myTriangles[i]]);
            AddLine(newMesh, newMeshBackground, MakeQuad(s, e, lineSize, lineSizeBackground), false);
        }
        SetMeshData(newMesh, newMeshBackground);

        //Restore scale and rotation
        transform.localScale = startScale;
        transform.rotation = startRotation;
        transform.parent = startParent;
    }

    void Update()
    {
        myMaterial.color = myColor;
        myMaterialBackground.color = myColorBackground;
        Draw();
    }

    void Draw()
    {
        if (drawBackground)
        {
            Graphics.DrawMesh(newMeshBackground, transform.localToWorldMatrix, myMaterialBackground, 0);
            Graphics.DrawMesh(newMesh, transform.localToWorldMatrix, myMaterial, 0);

        }
        else
        {

            Graphics.DrawMesh(newMeshBackground, transform.localToWorldMatrix, myMaterial, 0);
        }
    }

    Vector3[] MakeQuad(Vector3 s, Vector3 e, float w, float w2)
    {
        Vector3[] q = new Vector3[8];

        Vector3 l = -Vector3.Cross(s, e);
        l.Normalize();

        q[0] = transform.InverseTransformPoint(s + l * w);
        q[1] = transform.InverseTransformPoint(s + l * -w);
        q[2] = transform.InverseTransformPoint(e + l * w);
        q[3] = transform.InverseTransformPoint(e + l * -w);

        q[4] = transform.InverseTransformPoint(s + l * w2);
        q[5] = transform.InverseTransformPoint(s + l * -w2);
        q[6] = transform.InverseTransformPoint(e + l * w2);
        q[7] = transform.InverseTransformPoint(e + l * -w2);

        return q;
    }

    int verticesIndex;
    Vector3[] verticesOne;
    Vector3[] verticesTwo;
    int trianglesIndex;
    int[] trianglesOne;
    int[] trianglesTwo;

    void AddLine(Mesh m, Mesh m2, Vector3[] quad, bool tmp)
    {

        verticesOne[verticesIndex] = transform.position + quad[0];
        verticesOne[verticesIndex + 1] = transform.position + quad[1];
        verticesOne[verticesIndex + 2] = transform.position + quad[2];
        verticesOne[verticesIndex + 3] = transform.position + quad[3];


        verticesTwo[verticesIndex] = transform.position + quad[4];
        verticesTwo[verticesIndex + 1] = transform.position + quad[5];
        verticesTwo[verticesIndex + 2] = transform.position + quad[6];
        verticesTwo[verticesIndex + 3] = transform.position + quad[7];
        verticesIndex += 4;

        trianglesOne[trianglesIndex] = verticesIndex;
        trianglesOne[trianglesIndex + 1] = verticesIndex + 1;
        trianglesOne[trianglesIndex + 2] = verticesIndex + 2;
        trianglesOne[trianglesIndex + 3] = verticesIndex + 1;
        trianglesOne[trianglesIndex + 4] = verticesIndex + 3;
        trianglesOne[trianglesIndex + 5] = verticesIndex + 2;

        trianglesTwo[trianglesIndex] = verticesIndex;
        trianglesTwo[trianglesIndex + 1] = verticesIndex + 1;
        trianglesTwo[trianglesIndex + 2] = verticesIndex + 2;
        trianglesTwo[trianglesIndex + 3] = verticesIndex + 1;
        trianglesTwo[trianglesIndex + 4] = verticesIndex + 3;
        trianglesTwo[trianglesIndex + 5] = verticesIndex + 2;

        trianglesIndex += 6;
    }

    void SetMeshData(Mesh m1, Mesh m2)
    {
        m1.vertices = verticesOne;
        m1.triangles = trianglesOne;
        m1.RecalculateBounds();

        m2.vertices = verticesTwo;
        m2.triangles = trianglesTwo;
        m2.RecalculateBounds();
    }

}