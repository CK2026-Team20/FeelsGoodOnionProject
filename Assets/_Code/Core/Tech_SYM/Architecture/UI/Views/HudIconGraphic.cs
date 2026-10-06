using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Cooked.UI
{
    // Small vector HUD artwork. No source texture/import changes or runtime asset allocation.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudIconGraphic : MaskableGraphic
    {
        public enum Symbol { Heart, Tear, Form, Recover }
        [Tooltip("이 HUD에 그릴 아이콘 모양입니다. 체력·눈물·상태 전환·회수 중 화면 목적에 맞는 모양을 선택하세요.")]
        [SerializeField] private Symbol symbol;
        [Tooltip("체력 아이콘의 채워진 비율입니다(0~1). 0은 비어 있음, 1은 가득 참입니다. 런타임 HUD가 체력에 맞춰 갱신하며 다른 모양에는 체력 채우기를 적용하지 않습니다.")]
        [SerializeField, Range(0, 1)] private float fillAmount = 1;
        public float FillAmount
        {
            get => fillAmount;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(fillAmount, value)) return; fillAmount = value; SetVerticesDirty(); }
        }
        public void Configure(Symbol value) { symbol = value; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var contour = new List<Vector2>(64);
            for (int i = 0; i < 64; i++)
            {
                float t = i * Mathf.PI * 2 / 64;
                if (symbol == Symbol.Heart)
                    contour.Add(new Vector2(16 * Mathf.Pow(Mathf.Sin(t), 3) / 36,
                        (13 * Mathf.Cos(t) - 5 * Mathf.Cos(2*t) - 2 * Mathf.Cos(3*t) - Mathf.Cos(4*t)) / 36));
                else if (symbol == Symbol.Tear)
                    contour.Add(new Vector2(Mathf.Sin(t) * .34f * (1 - .65f * Mathf.Cos(t)), .46f * Mathf.Cos(t)));
                else
                    contour.Add(new Vector2(Mathf.Sin(t) * .33f, Mathf.Cos(t) * .3f - .06f));
            }
            var tint = symbol == Symbol.Heart ? new Color(1,.17f,.27f) : symbol == Symbol.Tear ? new Color(.06f,.72f,1) : new Color(1,.97f,.84f);
            Polygon(vh, contour, symbol == Symbol.Heart ? new Color(.2f,.08f,.1f,.8f) : tint);
            if (symbol == Symbol.Heart && fillAmount > 0) Polygon(vh, Clip(contour, -.46f + .92f * fillAmount), tint);
            Stroke(vh, contour, new Color(1,1,1,.9f), .018f, true);
            if (symbol == Symbol.Form)
            {
                Stroke(vh, new[]{new Vector2(0,.18f),new Vector2(-.09f,.40f),new Vector2(.03f,.34f)}, new Color(.65f,.94f,.58f), .04f, false);
                Stroke(vh, new[]{new Vector2(.39f,-.25f),new Vector2(.39f,.32f)}, Color.white,.025f,false);
                Stroke(vh, new[]{new Vector2(.32f,.24f),new Vector2(.39f,.32f),new Vector2(.46f,.24f)}, Color.white,.025f,false);
                Stroke(vh, new[]{new Vector2(.32f,-.17f),new Vector2(.39f,-.25f),new Vector2(.46f,-.17f)}, Color.white,.025f,false);
                Stroke(vh, new[]{new Vector2(-.12f,-.03f),new Vector2(-.12f,-.1f)}, new Color(.15f,.2f,.15f),.03f,false);
                Stroke(vh, new[]{new Vector2(.12f,-.03f),new Vector2(.12f,-.1f)}, new Color(.15f,.2f,.15f),.03f,false);
            }
            else if (symbol == Symbol.Recover)
            {
                var arc = new List<Vector2>();
                for (int i=0;i<25;i++) { float t = Mathf.Lerp(-.3f, 4.2f, i/24f); arc.Add(new Vector2(Mathf.Cos(t)*.43f,Mathf.Sin(t)*.4f)); }
                Stroke(vh,arc,new Color(.62f,.94f,.68f),.04f,false);
                var end=arc[arc.Count-1]; Stroke(vh,new[]{end+new Vector2(-.02f,.13f),end,end+new Vector2(.13f,.04f)},Color.white,.025f,false);
                Stroke(vh,new[]{new Vector2(-.15f,-.2f),new Vector2(-.08f,.12f),new Vector2(.07f,-.19f),new Vector2(.15f,.1f)},new Color(.67f,.47f,.23f),.024f,false);
            }
            else if (symbol == Symbol.Tear)
                Stroke(vh,new[]{new Vector2(-.17f,-.2f),new Vector2(-.21f,-.08f),new Vector2(-.18f,.04f)},new Color(1,1,1,.85f),.035f,false);
        }
        private Vector2 Point(Vector2 p)
        { var r=GetPixelAdjustedRect(); float size=Mathf.Min(r.width,r.height); return r.center+p*size; }
        private void Polygon(VertexHelper vh,IList<Vector2> points,Color tint)
        {
            if(points.Count<3)return;int first=vh.currentVertCount;var center=Vector2.zero;
            foreach(var p in points)center+=p;center/=points.Count;
            vh.AddVert(Point(center),tint*color,Vector2.zero);
            foreach(var p in points)vh.AddVert(Point(p),tint*color,Vector2.zero);
            for(int i=0;i<points.Count;i++)vh.AddTriangle(first,first+i+1,first+(i+1)%points.Count+1);
        }
        private void Stroke(VertexHelper vh,IList<Vector2> points,Color tint,float width,bool closed)
        {
            int count=closed?points.Count:points.Count-1;
            for(int i=0;i<count;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Count];var delta=(b-a).normalized;var n=new Vector2(-delta.y,delta.x)*width/2;
                int at=vh.currentVertCount;vh.AddVert(Point(a+n),tint*color,Vector2.zero);vh.AddVert(Point(b+n),tint*color,Vector2.zero);
                vh.AddVert(Point(b-n),tint*color,Vector2.zero);vh.AddVert(Point(a-n),tint*color,Vector2.zero);
                vh.AddTriangle(at,at+1,at+2);vh.AddTriangle(at,at+2,at+3);
            }
        }
        private static List<Vector2> Clip(IList<Vector2> input,float right)
        {
            var result=new List<Vector2>();var previous=input[input.Count-1];
            foreach(var current in input)
            {
                bool a=previous.x<=right,b=current.x<=right;
                if(a!=b)result.Add(Vector2.Lerp(previous,current,(right-previous.x)/(current.x-previous.x)));
                if(b)result.Add(current);previous=current;
            }
            return result;
        }
    }
}
