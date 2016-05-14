﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ViewController : MonoBehaviour {

    public GameObject viewPrefab;
    public GameObject UIObject;

    private Vector3 viewOrigScale;
    private LinkedList<ViewBehavior> viewList = new LinkedList<ViewBehavior>();
    private GameObject viewsObject;
    
	// Use this for initialization
	void Start () {
        viewsObject = new GameObject("Views");
        viewsObject.transform.parent = transform;

        viewOrigScale = viewPrefab.transform.localScale;
	}
	
	// Update is called once per frame
	void Update () {
	    if (Input.GetKeyDown(KeyCode.F1)) {
            createView();
        } else if (Input.GetKeyDown(KeyCode.F2)) {
            rearrange();
        } else if (Input.GetKeyDown(KeyCode.F3)) {
            showViewUI();
        }
	}

    void createView() {
        GameObject newView = Instantiate(viewPrefab);
        newView.transform.parent = viewsObject.transform;
        viewList.AddLast(newView.GetComponent<ViewBehavior>());
        rearrange();
    }

    void rearrange() {
        UIObject.SetActive(false);

        showSurroundedViews(viewList.Take(6), viewOrigScale, 360 / 6);
        foreach (ViewBehavior view in viewList.Skip(6)) {
            view.gameObject.SetActive(false);
        }
    }

    void showViewUI() {
        UIObject.SetActive(true);

        Vector3 previewScale = new Vector3(0.28f, 0.28f, 1f);
        showSurroundedViews(viewList.Take(6), previewScale, 120 / 6, -22f);
        showSurroundedViews(viewList.Skip(6), previewScale, 120 / 6, 27f);
    }

    void showSurroundedViews(IEnumerable<ViewBehavior> showList, Vector3 scale, float deltaAngle, float elevationAngle = 0f) {
        float firstAngle = -(showList.Count() - 1) / 2f * deltaAngle;
        foreach (ViewBehavior view in showList) {
            view.gameObject.SetActive(true);
            view.transform.localScale = scale;
            view.transform.eulerAngles = new Vector3(elevationAngle, firstAngle, 0);
            firstAngle += deltaAngle;
        }
    }
}
