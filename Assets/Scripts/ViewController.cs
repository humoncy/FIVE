﻿using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ViewController : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, IDragHandler {
	public SteamVR_TrackedController controller;
    public ViveSpinScroll spinController;
    public GameObject viewPrefab;
    public GameObject UIObject;
    public float shiftSpeed;

    private Camera mainCam;
    private Vector3 viewOrigScale;
    private LinkedList<ViewBehavior> displayedViewList = new LinkedList<ViewBehavior>();
    private LinkedList<ViewBehavior> hiddenViewList = new LinkedList<ViewBehavior>();
    private GameObject viewsObject;
    private ViewBehavior focusView = null;

    struct DraggingView {
        public Transform transform;
        public ViewBehavior viewBehavior;
        public LinkedList<ViewBehavior> selfList, otherList;
        public int index;

        public Dictionary<ViewBehavior, Vector3> shiftAngleDestination;
        public int newIndex;
        public LinkedList<ViewBehavior> newList;

        public float farAngle;
    }

	private DraggingView draggingView;
	private float gripTime = 0;

    private bool _isInUI;
    private bool isInUI {
        get { return _isInUI; }
        set {
            _isInUI = value;
            if (value)
                showViewUI();
            else
                rearrange();
        }
    }

    private bool _isEditing;
    private bool isEditing {
        get { return _isEditing; }
        set {
            foreach (ViewBehavior view in displayedViewList.Concat(hiddenViewList)) {
                view.isShaking = value;
            }
            _isEditing = value;
        }
    }

    // Use this for initialization
    void Start () {
        viewsObject = new GameObject("Views");
        viewsObject.transform.parent = transform;
        mainCam = Camera.main;
        
        viewOrigScale = viewPrefab.transform.localScale;
        isInUI = false;

		listenGrip ();
        listenEditorController();
        MenuController.addBtn("Open File (Create View)", () => {
			FileManager.fileBrowserCallback = createView;
			FileManager.fileBrowserStatus = FileManager.FileBrowserStatus.File;
        });
		MenuController.addBtn("Toggle View Management", () => {
			isInUI = !isInUI;
		});
		MenuController.addBtn("Toggle View Editing", () => {
			if (isInUI)
				isEditing = !isEditing;
		});
	}
	
	// Update is called once per frame
	void Update () {
	    if (Input.GetKeyDown(KeyCode.F1)) {
            FileManager.fileBrowserCallback = createView;
            FileManager.fileBrowserStatus = FileManager.FileBrowserStatus.File;
        } else if (Input.GetKeyDown(KeyCode.F2)) {
            isInUI = !isInUI;
        } else if (Input.GetKeyDown(KeyCode.F3)) {
            if (isInUI) {
                isEditing = !isEditing;
            }
        } else if (Input.GetKeyDown(KeyCode.F9)) {
            if (focusView != null) {
                focusView.enlargeFont(-2);
            }
        } else if (Input.GetKeyDown(KeyCode.F10)) {
            if (focusView != null) {
                focusView.enlargeFont(2);
            }
        } else if (Input.GetKeyDown(KeyCode.F7)) {
            if (focusView != null) {
                focusView.scroll(-1);
            }
        } else if (Input.GetKeyDown(KeyCode.F8)) {
            if (focusView != null) {
                focusView.scroll(1);
            }
        }

		updateGrip ();
    }

    void LateUpdate() {
        if (!isInUI)
            setFocus();
	}

	public void createView(string filePath) {
        ViewBehavior newView = Instantiate(viewPrefab).GetComponent<ViewBehavior>();
        newView.transform.parent = viewsObject.transform;
        newView.viewController = this;
        newView.selfNode = displayedViewList.Count < 6 ? displayedViewList.AddLast(newView) : hiddenViewList.AddLast(newView);
        rearrange();

        newView.fileOpened.view = newView;
        newView.fileOpened.loadFile(filePath);
    }
    
    public void removeView(LinkedListNode<ViewBehavior> view) {
        Destroy(view.Value.gameObject);
        view.List.Remove(view);
        showViewUI();
    }

    void rearrange() {
        UIObject.SetActive(false);
        isEditing = false;
        _isInUI = false;

        showSurroundedViews(displayedViewList, viewOrigScale, 360 / 6);
        foreach (ViewBehavior view in hiddenViewList) {
            view.gameObject.SetActive(false);
        }
    }

    void showViewUI() {
        UIObject.SetActive(true);
        _isInUI = true;

        Vector3 previewScale = new Vector3(0.28f, 0.28f, 1f);
        showSurroundedViews(displayedViewList, previewScale, 120 / 6, -22f);
        showSurroundedViews(hiddenViewList, previewScale, 120 / 6, 27f);
    }

    void showSurroundedViews(IEnumerable<ViewBehavior> showList, Vector3 scale, float deltaAngle, float elevationAngle = 0f) {
        float firstAngle = -(showList.Count() - 1) / 2f * deltaAngle;
        foreach (ViewBehavior view in showList) {
            view.gameObject.SetActive(true);
            view.transform.localScale = scale;
            view.transform.eulerAngles = new Vector3(elevationAngle, firstAngle, 0);
            firstAngle += deltaAngle;
        }

        if (focusView != null) {
            focusView.selected = false;
            focusView = null;
        }
	}

	void listenGrip() {
		controller.Gripped += (object sender, ClickedEventArgs e) => {
			gripTime = Time.time;
		};
		controller.Ungripped += (object sender, ClickedEventArgs e) => {
			if (gripTime != -1f)
				isInUI = !isInUI;
		};
	}

    void listenEditorController() {
        controller.PadClicked += (object sender, ClickedEventArgs e) => {
            if (focusView != null && Mathf.Abs(e.padX) < 0.5f) {
                focusView.enlargeFont(
                    e.padY > 0f ? 2 : -2
                );
            }
        };

        spinController.SpinScrolled += (int amount) => {
            if (focusView != null) {
                focusView.scroll(amount);
            }
        };
    }

	void updateGrip() {
		if (controller.gripped && isInUI && gripTime != -1f && Time.time - gripTime >= 0.5f) {
			isEditing = !isEditing;
			SteamVR_Controller.Input((int) controller.controllerIndex).TriggerHapticPulse(3999);
			gripTime = -1f;
		}
	}

	public void OnPointerDown(PointerEventData e) {
		if (isEditing)
			return;
		
		WebViewViveInput webView = e.pointerEnter.GetComponent<WebViewViveInput> ();
		if (webView != null)
			webView.OnPointerDown ();
	}

	public void OnPointerUp(PointerEventData e) {
		if (isEditing)
			return;
		
		WebViewViveInput webView = e.pointerEnter.GetComponent<WebViewViveInput> ();
		if (webView != null)
			webView.OnPointerUp ();
	}

	public void OnPointerClick(PointerEventData e) {
		GameObject pointerOn = e.pointerEnter;
		if (!MenuController.isActive && isEditing && pointerOn.CompareTag ("ViewClose")) {
			ViewBehavior view = pointerOn.transform.parent.parent.gameObject.GetComponent<ViewBehavior> ();
			if (displayedViewList.Concat (hiddenViewList).Contains (view))
				view.OnCloseBtnPressed ();
		}
	}

	public void OnBeginDrag(PointerEventData e) {
		GameObject pointerOn = e.pointerEnter;
		if (!MenuController.isActive && isEditing && pointerOn.CompareTag("ViewPlane")) {
			draggingView.transform = pointerOn.transform.parent;
			draggingView.viewBehavior = pointerOn.transform.parent.GetComponent<ViewBehavior>();
			draggingView.selfList = draggingView.viewBehavior.selfNode.List;
			draggingView.otherList = draggingView.selfList == displayedViewList ? hiddenViewList : displayedViewList;
			draggingView.index = draggingView.selfList.Select((item, inx) => new { item, inx }).First(x => x.item == draggingView.viewBehavior).inx;
			draggingView.shiftAngleDestination = new Dictionary<ViewBehavior, Vector3>();
		}
	}

	public void OnEndDrag(PointerEventData e) {
		if (draggingView.transform != null) {
			commitDraggingResult();
			draggingView.transform = null;
		}
	}

	public void OnDrag(PointerEventData e) {
		if (draggingView.transform == null)
			return;
		draggingView.transform.rotation = Quaternion.FromToRotation (Vector3.forward, e.pointerCurrentRaycast.worldPosition);

        float elevationAngle = draggingView.transform.eulerAngles.x;
        float leftRightAngle = draggingView.transform.eulerAngles.y;
        float standEleAngle = draggingView.selfList == displayedViewList ? -22f : 27f;
        float otherEleAngle = standEleAngle == -22f ? 27f : -22f;
		float newLRAngle;

		if (Mathf.Abs(Mathf.DeltaAngle(elevationAngle, standEleAngle)) > 20) {
            // View has been dragged out of its original line (displayed or hidden).
            bool displayExceed = false;

			if (Mathf.Abs(Mathf.DeltaAngle(elevationAngle, otherEleAngle)) < 20) {
                // View has been dragged to the other line, i.e. displayed -> hidden or hidden -> displayed
                // Views in **the other line** shall shift to new angles.
                displayExceed = draggingView.selfList != displayedViewList && displayedViewList.Count == 6;
                newLRAngle = -(draggingView.otherList.Count() - (displayExceed ? 1 : 0)) / 2f * 20;

                int newIndex = Mathf.RoundToInt(Mathf.DeltaAngle(newLRAngle, leftRightAngle) / 20);
                if (newIndex < 0)
                    newIndex = 0;
                else if (displayExceed && newIndex >= 6)
                    newIndex = 5;
                else if (newIndex > draggingView.otherList.Count)
                    newIndex = draggingView.otherList.Count;

                foreach (var view in draggingView.otherList.Take(newIndex)) {
                    draggingView.shiftAngleDestination[view] = new Vector3(otherEleAngle, newLRAngle, 0);
                    newLRAngle += 20f;
                }
                newLRAngle += 20f;
                foreach (var view in draggingView.otherList.Skip(newIndex)) {
                    draggingView.shiftAngleDestination[view] = new Vector3(otherEleAngle, newLRAngle, 0);
                    newLRAngle += 20f;
                }

                // View is dragged to the other line, with new index.
                draggingView.newIndex = newIndex;
                draggingView.newList = draggingView.otherList;
            } else {
                // View is dragged out of original line but not in the other line, so keep original index and line.
                draggingView.newIndex = draggingView.index;
                draggingView.newList = draggingView.selfList;
            }

            // Other views in **original line** shall shift to new angles.
            if (displayExceed) {
                newLRAngle = -(draggingView.selfList.Count() - 1) / 2f * 20;
                Vector3 oldAngle = draggingView.shiftAngleDestination[displayedViewList.Last()];
                Vector3 newAngle = new Vector3(standEleAngle, newLRAngle, 0);
                draggingView.farAngle = Mathf.Abs(Quaternion.Angle(Quaternion.Euler(oldAngle), Quaternion.Euler(newAngle)));
                draggingView.shiftAngleDestination[displayedViewList.Last()] = newAngle;
                newLRAngle += 20f;
            } else {
                newLRAngle = -(draggingView.selfList.Count() - 2) / 2f * 20;
            }

            foreach (var view in draggingView.selfList) {
                if (view != draggingView.viewBehavior) {
                    draggingView.shiftAngleDestination[view] = new Vector3(standEleAngle, newLRAngle, 0);
                    newLRAngle += 20f;
                }
            }
        } else {
            // View is not dragged out of original line, but may change its index, i.e. displayed -> displayed or hidden -> hidden
            // Other views in **original line** shall shift to new angles.
			newLRAngle = -(draggingView.selfList.Count() - 1) / 2f * 20;
			int newIndex = Mathf.RoundToInt(Mathf.DeltaAngle(newLRAngle, leftRightAngle) / 20);
            if (newIndex < 0)
                newIndex = 0;
            else if (newIndex > draggingView.selfList.Count - 1)
                newIndex = draggingView.selfList.Count - 1;

            foreach (var view in draggingView.selfList.Take(newIndex + (newIndex > draggingView.index ? 1 : 0))) {
                if (view != draggingView.viewBehavior) {
                    draggingView.shiftAngleDestination[view] = new Vector3(standEleAngle, newLRAngle, 0);
                    newLRAngle += 20f;
                }
            }
            newLRAngle += 20f;
			foreach (var view in draggingView.selfList.Skip(newIndex + (newIndex > draggingView.index ? 1 : 0))) {
                if (view != draggingView.viewBehavior) {
                    draggingView.shiftAngleDestination[view] = new Vector3(standEleAngle, newLRAngle, 0);
                    newLRAngle += 20f;
                }
            }

            draggingView.newIndex = newIndex;
            draggingView.newList = draggingView.selfList;
        }

		if (draggingView.newList != draggingView.otherList) {
            // If angles of views in **other line** have not been updated,
            // Update their angles to make sure all work correctly.
			newLRAngle = -(draggingView.otherList.Count () - 1) / 2f * 20;
			foreach (var view in draggingView.otherList) {
				draggingView.shiftAngleDestination [view] = new Vector3 (otherEleAngle, newLRAngle, 0);
				newLRAngle += 20f;
			}
		}

        updateAllViewsWhenDragging();
	}

    void updateAllViewsWhenDragging() {
        foreach (var view in draggingView.shiftAngleDestination) {
            Vector3 originalAngle = view.Key.transform.eulerAngles;
			if (Mathf.Abs(Mathf.DeltaAngle(originalAngle.x, view.Value.x)) < 0.001f) {
                originalAngle.y = Mathf.MoveTowardsAngle(originalAngle.y, view.Value.y, Time.deltaTime * shiftSpeed);
                view.Key.transform.eulerAngles = originalAngle;
            } else {
                view.Key.transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(originalAngle), Quaternion.Euler(view.Value), Time.deltaTime * shiftSpeed / 20f * draggingView.farAngle);
            }
        }
    }

    void commitDraggingResult() {
        if (draggingView.newList != draggingView.selfList || draggingView.newIndex != draggingView.index) {
            draggingView.selfList.Remove(draggingView.viewBehavior.selfNode);

			if (draggingView.newIndex >= draggingView.newList.Count)
				draggingView.newList.AddLast (draggingView.viewBehavior.selfNode);
			else
				draggingView.newList.AddBefore(draggingView.newList.ElementAt(draggingView.newIndex).selfNode, draggingView.viewBehavior.selfNode);

            if (displayedViewList.Count > 6) {
                LinkedListNode<ViewBehavior> node = displayedViewList.Last;
                displayedViewList.RemoveLast();
                hiddenViewList.AddFirst(node);
            }
        }

        showViewUI();
    }

    void setFocus() {
        IEnumerable<ViewBehavior> nextFocus = 
            from view in displayedViewList
            where view != focusView
            where Mathf.Abs(Mathf.DeltaAngle(view.transform.eulerAngles.y, mainCam.transform.eulerAngles.y)) < 30f
            select view;

        if (nextFocus.Count() != 0) {
            if (focusView != null)
                focusView.selected = false;

            focusView = nextFocus.First();
            focusView.selected = true;
            focusView.webView.FocusView();
        }
    }
}
