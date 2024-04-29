using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO;
using C2M2.NeuronalDynamics.Interaction;
using System.Linq;
using TMPro;
public class Placed : MonoBehaviour
{

    private static GameObject cellPreviewerObj;
    private static CellPreviewer cellPreviewer;
    public static GameObject[] neuronArray;
    public static int cellSize;
    public static int Page;
    public static GameObject UpArrow;
    public static GameObject DownArrow;
    public static GameObject refresh;
    public static GameObject pageCounter;

    public void removeNeuroPreview()
    {
        for (int i = cellPreviewerObj.transform.childCount - 1; i > 0; --i)
        {
            Destroy(cellPreviewerObj.transform.GetChild(i).gameObject);
        }

        cellPreviewer.generateNeuron();
        for (int i = 1; i < cellPreviewerObj.transform.childCount; ++i)
        {
            neuronArray.Append(cellPreviewerObj.transform.GetChild(i).gameObject);
        }
    }
    public void pressButton()
    {
       if (cellPreviewer.newFiles.Count > 0)
        {
            foreach (FileInfo i in cellPreviewer.newFiles)
            {
                cellPreviewer.files.Add(i);
            }
            UnityEngine.Debug.Log(cellPreviewer.newFiles.Count);
            cellPreviewer.newFiles.Clear();
            cellPreviewer.files.Sort((file1, file2) => file1.Name.CompareTo(file2.Name));
            UnityEngine.Debug.Log(cellPreviewer.files.GetRange(0, cellPreviewer.files.Count));

        }
       cellSize = cellPreviewerObj.GetComponent<CellPreviewer>().positionsNorm.Length * 3;
        removeNeuroPreview();
        changePageNumbers(Page, (int)Math.Ceiling((double)cellPreviewer.files.Count / cellSize));
    }
    public void changePageNumbers(int currentPage, int maxPage)
    {
        pageCounter.GetComponent<TextMeshPro>().text = String.Format("<align=center><size=100%>{0}</size>\n" +
                           "<line-height=0em>\n</line-height>" +
                           "<size=80%><u>__</u></size>\n" +
                           "<line-height=1em>\n</line-height>" +
                           "<size=100%>{1}</size></align>", currentPage + 1, maxPage);
    }

    public static void makePreviewerControlsVisible(bool hide)
    {
        UpArrow.SetActive(hide);
        DownArrow.SetActive(hide);
        refresh.SetActive(hide);
        pageCounter.SetActive(hide);
    }

    public void downward()
    {

        var doesNextIndexExist = cellPreviewer.files.ElementAtOrDefault((Page + 1) * 9) != null;
        if (doesNextIndexExist)
        {
            Page += 1;
            changePageNumbers(Page, (int)Math.Ceiling((double)cellPreviewer.files.Count / cellSize));
            removeNeuroPreview();
        }
        
    }


    public void upward()
    {
        if (Page - 1 >= 0)
        {
            Page -= 1;
            changePageNumbers(Page, (int)Math.Ceiling((double)cellPreviewer.files.Count / cellSize));
            removeNeuroPreview();

        }
    }
     void Start()
    {


        cellPreviewerObj = GameObject.FindGameObjectWithTag("CellPreviewer");
        cellPreviewer = cellPreviewerObj.GetComponent<CellPreviewer>();
        neuronArray = GameObject.FindGameObjectsWithTag("Neuron");
        cellSize = cellPreviewerObj.GetComponent<CellPreviewer>().positionsNorm.Length * 3;
        UpArrow = GameObject.Find("UpArrow");
        DownArrow = GameObject.Find("DownArrow");
        refresh = GameObject.Find("Refresh");
        
        Page = 0;
        pageCounter = GameObject.Find("Page Counter");
        StartCoroutine(DelayedPageStart(0.1f));

    }

    IEnumerator DelayedPageStart(float delayInSeconds)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delayInSeconds);
        changePageNumbers(Page, (int)Math.Ceiling((double)cellPreviewer.files.Count / cellSize));


    }
}



