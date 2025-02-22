using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ImageController : MonoBehaviour
{
 public Image[] images;

    void Start()
    {
        HideAllImages(); // 시작할 때 모든 이미지 숨기기
    }

    public void ShowImage(int index)
    {
        HideAllImages(); // 다른 이미지는 숨김
        if (index >= 1 && index <= images.Length)
        {
            images[index - 1].enabled = true;
            StartCoroutine(HideImageAfterDelay(images[index - 1], 2f));
        }
    }

    private IEnumerator HideImageAfterDelay(Image image, float delay)
    {
        yield return new WaitForSeconds(delay);
        image.enabled = false;
    }

    private void HideAllImages()
    {
        foreach (Image img in images)
        {
            img.enabled = false;
        }
    }

}
