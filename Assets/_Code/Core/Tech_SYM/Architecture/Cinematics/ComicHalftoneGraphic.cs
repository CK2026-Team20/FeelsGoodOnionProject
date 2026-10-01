using UnityEngine;
using UnityEngine.UI;
namespace Cooked.Cinematics
{
    // Decoration only; page-space circles avoid texture/import dependencies and intercept no input.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComicHalftoneGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r=GetPixelAdjustedRect();
            const float gap=28, radius=3;
            int row=0;
            for(float y=r.yMin+gap*.5f;y<r.yMax;y+=gap,row++)
            for(float x=r.xMin+gap*.5f+(row%2)*gap*.5f;x<r.xMax;x+=gap)
            {
                int start=mesh.currentVertCount;
                mesh.AddVert(new Vector3(x,y),color,Vector2.zero);
                for(int n=0;n<8;n++){float a=n*Mathf.PI*.25f;mesh.AddVert(new Vector3(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),color,Vector2.zero);}
                for(int n=0;n<8;n++)mesh.AddTriangle(start,start+1+n,start+1+(n+1)%8);
            }
        }
    }
}
