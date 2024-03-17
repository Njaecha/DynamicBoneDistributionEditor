using ADV.Commands.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicBoneDistributionEditor
{
    internal class DBDEGizmoController : MonoBehaviour
    {
        public DBDEDynamicBoneEdit Editing;

        private Material _gizmoMaterial;

        void Awake( )
        {
            // Unity has a built-in shader that is useful for drawing simple colored things.
            var shader = Shader.Find("Hidden/Internal-Colored");
            _gizmoMaterial = new Material(shader);
            _gizmoMaterial.hideFlags = HideFlags.HideAndDontSave;

            // Turn on alpha blending
            _gizmoMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _gizmoMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            // Always draw
            _gizmoMaterial.SetInt("_Cull", (int)CullMode.Off);
            _gizmoMaterial.SetInt("_ZWrite", 0);
            _gizmoMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        void OnPostRender()
        {
            if (!DBDE.drawGizmos.Value) return;
            if (Editing == null || Editing.DynamicBones == null || Editing.PrimaryDynamicBone?.m_Root == null) return;
            _gizmoMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);

            GL.Begin( GL.LINE_STRIP );
            GL.Color(Color.magenta);
            Transform leaf = traverseBones(Editing.PrimaryDynamicBone.m_Root.transform);
            GL.End();

            Vector3 gTip = drawArrow(leaf.position, Editing.gravity.value, Color.blue, 50);
            Vector3 fTip = drawArrow(gTip, Editing.force.value, Color.red, 50);
            drawArrow(leaf.position, fTip - leaf.position, Color.green, 1);

            GL.PopMatrix();
        }

        private Transform traverseBones(Transform transform)
        {
            GL.Vertex(transform.position);
            if (transform.childCount > 0)
            {
                return traverseBones(transform.GetChild(0));
            }
            else return transform;
        }

        private Vector3 drawArrow(Vector3 Base, Vector3 relativeTip, Color color, int factor)
        {
            GL.Begin (GL.LINES);
            GL.Color(color);
            GL.Vertex(Base);
            Vector3 tip = Base + relativeTip * factor;
            GL.Vertex(tip);
            GL.End ();
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            List<Vector3> cornerpoints = CalculateBasePoints(tip, Base + relativeTip * (factor * 0.8f));
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[0]);
            GL.Vertex(cornerpoints[1]);
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[1]);
            GL.Vertex(cornerpoints[2]); 
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[2]);
            GL.Vertex(cornerpoints[3]);
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[3]);
            GL.Vertex(cornerpoints[0]);
            GL.End();
            return tip;
        }

        // AI generated :)
        private List<Vector3> CalculateBasePoints(Vector3 tip, Vector3 baseCenter)
        {
            Vector3 c = tip - baseCenter;
            Vector3 pA = Vector3.Cross(Vector3.forward, c)*0.35f;
            Vector3 pB = Quaternion.AngleAxis(90, c) * pA;
            Vector3 pC = Quaternion.AngleAxis(180, c) * pA;
            Vector3 pD = Quaternion.AngleAxis(270, c) * pA;

            return new List<Vector3>() { baseCenter+pA, baseCenter+pB, baseCenter + pC, baseCenter + pD };
        }
    }
}
