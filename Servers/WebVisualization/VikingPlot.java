/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */

/*
 * VikingForm.java
 *
 * Created on Oct 12, 2010, 3:24:48 AM
 */

//package vikingplot3d;

import java.io.FileNotFoundException;
import java.net.MalformedURLException;
import java.util.Enumeration;
import java.util.Hashtable;
import java.awt.*;
import java.util.logging.Level;
import java.util.logging.Logger;
import java.net.URL;
import javax.swing.*;


import com.sun.j3d.utils.behaviors.vp.OrbitBehavior;
import com.sun.j3d.utils.universe.PlatformGeometry;
import com.sun.j3d.utils.universe.SimpleUniverse;
import com.sun.j3d.utils.universe.ViewingPlatform;
import com.sun.j3d.loaders.IncorrectFormatException;
import com.sun.j3d.loaders.ParsingErrorException;
import com.sun.j3d.loaders.Scene;
import com.sun.j3d.loaders.objectfile.ObjectFile;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.URLConnection;
import java.util.ArrayList;
import java.util.regex.Pattern;
import javax.media.j3d.*;
import javax.vecmath.Color3f;
import javax.vecmath.Point3d;
import javax.vecmath.Vector3f;




/**
 *
 * @author Shoeb
 */
public class VikingPlot extends JApplet  {

    String cells = "teapot";

    private boolean spin = false;
    private boolean noTriangulate = false;
    private boolean noStripify = false;
    private double creaseAngle = 60.0;

    private URL filename = null;

    private SimpleUniverse univ = null;
    private BranchGroup scene = null;
    private BranchGroup root = null;

    Boolean first = true;


    public int view = 0;
    public Scene s=null;
    BranchGroup objRoot = null;
    TransformGroup objTrans = null;
    BranchGroup teapot = null;

    ArrayList<BranchGroup> scenes = new ArrayList<BranchGroup>();

  public BranchGroup createSceneGraph() throws MalformedURLException {


	// Start a new branch group
        objRoot = new BranchGroup();
        objRoot.setCapability(BranchGroup.ALLOW_DETACH);

        TransformGroup objScale = new TransformGroup();
        Transform3D t3d = new Transform3D();
        t3d.setScale(0.75);
        objScale.setTransform(t3d);
        objRoot.addChild(objScale);


	objTrans = new TransformGroup();

	objTrans.setCapability(TransformGroup.ALLOW_TRANSFORM_WRITE);
	objTrans.setCapability(TransformGroup.ALLOW_TRANSFORM_READ);
	objScale.addChild(objTrans);

        int flags = ObjectFile.RESIZE;
	if (!noTriangulate) flags |= ObjectFile.TRIANGULATE;
	if (!noStripify) flags |= ObjectFile.STRIPIFY;
	ObjectFile f = new ObjectFile(flags,
	  (float)(creaseAngle * Math.PI / 180.0));


        try
        {
            URL request = new URL("http://connectomes.utah.edu/Test/VikingPlot/GetObjectFile?number=" + cells);
            URLConnection conn = request.openConnection();


            BufferedReader in = new BufferedReader(new InputStreamReader(conn.getInputStream()));
            filename = new URL(in.readLine());
        }
        catch (Exception e)
        {
            e.printStackTrace();
        }


	try {
	  s = f.load(filename);
	}
	catch (FileNotFoundException e) {
	  System.err.println(e);
	  System.exit(1);
	}
	catch (ParsingErrorException e) {
	  System.err.println(e);
	  System.exit(1);
	}
	catch (IncorrectFormatException e) {
	  System.err.println(e);
	  System.exit(1);
	}

        root = s.getSceneGroup();
        root.setCapability(BranchGroup.ALLOW_DETACH);

	objTrans.addChild(s.getSceneGroup());
	BoundingSphere bounds = new BoundingSphere(new Point3d(0.0,0.0,0.0), 100.0);

        if (spin) {
	  Transform3D yAxis = new Transform3D();
	  Alpha rotationAlpha = new Alpha(-1, Alpha.INCREASING_ENABLE,
					  0, 0,
					  4000, 0, 0,
					  0, 0, 0);

	  RotationInterpolator rotator =
	      new RotationInterpolator(rotationAlpha, objTrans, yAxis,
				       0.0f, (float) Math.PI*2.0f);
	  rotator.setSchedulingBounds(bounds);
	  objTrans.addChild(rotator);
	}


        Color3f bgColor = new Color3f(0.07f, 0.07f, 0.7f);
        Background bgNode = new Background(bgColor);
        bgNode.setApplicationBounds(bounds);
        objRoot.addChild(bgNode);


//        fc();

	return objRoot;
    }



    public void fc(){
      if (view != 0){


        Hashtable table = s.getNamedObjects();
        for (Enumeration e = table.keys() ; e.hasMoreElements() ;) {
          Object key = e.nextElement();
          System.out.println(key);
          Object obj = table.get(key);
          System.out.println(obj.getClass().getName());
          Shape3D shape  = (Shape3D)obj;

          //shape.setCapability(shape.ALLOW_APPEARANCE_OVERRIDE_WRITE);

          Appearance app = new Appearance();

          app = createAppearance(view);
          shape.setAppearance(app);
        }
      }
    }

    private Appearance createAppearance(int idx) {
	Appearance app = new Appearance();

	// Globalne farby.
	Color3f black = new Color3f(0.0f, 0.0f, 0.0f);
	Color3f white = new Color3f(1.0f, 1.0f, 1.0f);

	switch (idx) {
	// Unlit solid
	case 1:
	    {

	      Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              ColoringAttributes ca = new ColoringAttributes();
              ca.setColor(objColor);
              app.setColoringAttributes(ca);
              break;
	    }


	case 2:
	    {

              Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              ColoringAttributes ca = new ColoringAttributes();
              ca.setColor(objColor);
              app.setColoringAttributes(ca);

              PolygonAttributes pa = new PolygonAttributes();
              pa.setPolygonMode(pa.POLYGON_LINE);
              pa.setCullFace(pa.CULL_NONE);
              app.setPolygonAttributes(pa);
              break;
	    }

        // Unlit points (small points)
        case 3:
            {

              Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              ColoringAttributes ca = new ColoringAttributes();
              ca.setColor(objColor);
              app.setColoringAttributes(ca);


              PolygonAttributes pa = new PolygonAttributes();
              pa.setPolygonMode(pa.POLYGON_POINT);
              pa.setCullFace(pa.CULL_NONE);
              app.setPolygonAttributes(pa);


              PointAttributes pta = new PointAttributes();
              pta.setPointSize(1.0f);
	      app.setPointAttributes(pta);
              break;
            }
	// Unlit points (big points)
	case 4:
	    {

              Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              ColoringAttributes ca = new ColoringAttributes();
              ca.setColor(objColor);
              app.setColoringAttributes(ca);


              PolygonAttributes pa = new PolygonAttributes();
              pa.setPolygonMode(pa.POLYGON_POINT);
              pa.setCullFace(pa.CULL_NONE);
              app.setPolygonAttributes(pa);


              PointAttributes pta = new PointAttributes();
              pta.setPointSize(5.0f);
              app.setPointAttributes(pta);
              break;
	    }

	// Lit solid
	case 5:
	    {

              Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              app.setMaterial(new Material(objColor, black, objColor,
              			     white, 80.0f));
              break;
	    }

        // Another lit solid with a different color
	case 6:
	    {

              Color3f objColor = new Color3f(0.8f, 0.f, 0.0f);
              app.setMaterial(new Material(objColor, black, objColor,
              			     white, 80.0f));
              break;
	    }


	// Transparent, lit solid
	case 7:
	    {

              TransparencyAttributes ta = new TransparencyAttributes();
              ta.setTransparencyMode(ta.BLENDED);
              ta.setTransparency(0.6f);
              app.setTransparencyAttributes(ta);


              PolygonAttributes pa = new PolygonAttributes();
              pa.setCullFace(pa.CULL_NONE);
              app.setPolygonAttributes(pa);


              Color3f objColor = new Color3f(0.7f, 0.8f, 1.0f);
              app.setMaterial(new Material(objColor, black, objColor,
              			     black, 1.0f));
              break;
	    }

        // Sreen-Door Transparent, lit solid
        case 8:
            {

              TransparencyAttributes ta = new TransparencyAttributes();
              ta.setTransparencyMode(ta.SCREEN_DOOR);
              ta.setTransparency(0.5f);
              app.setTransparencyAttributes(ta);


              PolygonAttributes pa = new PolygonAttributes();
              pa.setCullFace(pa.CULL_NONE);
              app.setPolygonAttributes(pa);


              Color3f objColor = new Color3f(0.7f, 0.8f, 1.0f);
              app.setMaterial(new Material(objColor, black, objColor,
				     black, 1.0f));
              break;
            }
	// Lit solid, no specular
	case 9:
	    {

              Color3f objColor = new Color3f(0.7f, 0.7f, 0.0f);
              app.setMaterial(new Material(objColor, black, objColor,
              			     black, 80.0f));
              break;
	    }

	// Lit solid, specular only
	case 10:
	    {

              Color3f objColor = new Color3f(0.8f, 0.0f, 0.0f);
              app.setMaterial(new Material(black, black, black,
              			     white, 80.0f));
              break;
	    }

	}

	return app;
    }

    private Canvas3D createUniverse() {
        // Ziskanie preferovanej grafickej konfiguracie pre standardnu obrazovku
	GraphicsConfiguration config =
	    SimpleUniverse.getPreferredConfiguration();

	// Vytvorenie Canvas3D pomocou ziskanej konfiguracie obrazovky
	Canvas3D canvas3d = new Canvas3D(config);

        // Vytvorenie jednoducheho vesmiru pomocou view branch
	univ = new SimpleUniverse(canvas3d);
        BoundingSphere bounds = new BoundingSphere(new Point3d(0.0,0.0,0.0), 100.0);

        // Pridanie spravanie sa mysi do ViewingPlatformu
	ViewingPlatform viewingPlatform = univ.getViewingPlatform();

	PlatformGeometry pg = new PlatformGeometry();

	// Nastavenie ambient svetla
	Color3f ambientColor = new Color3f(0.1f, 0.1f, 0.1f);
	AmbientLight ambientLightNode = new AmbientLight(ambientColor);
	ambientLightNode.setInfluencingBounds(bounds);
	pg.addChild(ambientLightNode);

	// Nastavenie priameho svetla
	Color3f light1Color = new Color3f(1.0f, 1.0f, 0.9f);
	Vector3f light1Direction  = new Vector3f(1.0f, 1.0f, 1.0f);
	Color3f light2Color = new Color3f(1.0f, 1.0f, 1.0f);
	Vector3f light2Direction  = new Vector3f(-1.0f, -1.0f, -1.0f);

	DirectionalLight light1
	    = new DirectionalLight(light1Color, light1Direction);
	light1.setInfluencingBounds(bounds);
	pg.addChild(light1);

	DirectionalLight light2
	    = new DirectionalLight(light2Color, light2Direction);
	light2.setInfluencingBounds(bounds);
	pg.addChild(light2);

	viewingPlatform.setPlatformGeometry( pg );

        // Toto posunie ViewPlatform trocha dozadu, aby sa objekty
        // v scene dali vidiet
	viewingPlatform.setNominalViewingTransform();

	if (!spin) {
            OrbitBehavior orbit = new OrbitBehavior(canvas3d,
						    OrbitBehavior.REVERSE_ALL);
            orbit.setSchedulingBounds(bounds);
            viewingPlatform.setViewPlatformBehavior(orbit);
	}

	univ.getViewer().getView().setMinimumFrameCycleTime(5);

	return canvas3d;
    }



    /******************** GUI Code *****************************************/
    /** Creates new form VikingForm */
    @Override
    public void init() {
        //initialize the components in the GUI
        initComponents();


        try
        {
        webrequest = new URL("http://connectomes.utah.edu/Test/FormRequest/GetTopStructures?request=MarcLab(connectomes.utah.edu),Rabbit,1");
        }
        catch(Exception e)
        {
            e.printStackTrace();
        }
        new GetCells().execute();

        currentObject.setText(cells);

        Canvas3D c = createUniverse();
        c.setSize(400,600);
        mainPanel.setLayout(new BorderLayout());

        mainPanel.add("Center",c);
        mainPanel.setSize(new java.awt.Dimension(500,500));


        objectsLoading();



    }

    public void objectsLoading()
    {


        try {

            if(scene!=null)
            {
//                scene.detach();
//                univ.addBranchGraph(teapot);
            }


            scene = createSceneGraph();

            if(first)
            {
                first = false;
                teapot = scene;
            }

        } catch (MalformedURLException ex) {
            Logger.getLogger(VikingPlot.class.getName()).log(Level.SEVERE, null, ex);
        }
        if(teapot!=null)
            teapot.detach();

        scenes.add(scene);
	univ.addBranchGraph(scene);



    }

     public void stop()
      {
              univ.cleanup();
              filename = null;
      }

    DefaultComboBoxModel comboBoxModel;



    public class GetCells extends SwingWorker<String, Void>
    {
    @Override
    protected String doInBackground() {

         try
        {

            URLConnection conn = webrequest.openConnection();

            BufferedReader in = new BufferedReader(new InputStreamReader(conn.getInputStream()));

            Pattern p = Pattern.compile("\\[\\]\\\"\\ ");

            String value = in.readLine().replace("[","")
                                .replace("]","").replace("\"","");

            String[] values = value.split(",");


            comboBoxModel = new DefaultComboBoxModel(values);

            cellID.setModel(comboBoxModel);
        }
        catch(Exception e)
        {
            e.printStackTrace();
        }

         return "done";

    }

    @Override
    public void done() {

    }
}

    /** Creates new form VikingForm */

    /** This method is called from within the constructor to
     * initialize the form.
     * WARNING: Do NOT modify this code. The content of this method is
     * always regenerated by the Form Editor.
     */
    @SuppressWarnings("unchecked")
    // <editor-fold defaultstate="collapsed" desc="Generated Code">
      private void initComponents() {

        jInternalFrame1 = new javax.swing.JInternalFrame();
        jLabel1 = new javax.swing.JLabel();
        cellIDs = new javax.swing.JTextField();
        submitButton = new javax.swing.JButton();
        jLabel2 = new javax.swing.JLabel();
        cellID = new javax.swing.JComboBox();
        mainPanel = new javax.swing.JPanel();
        jLabel3 = new javax.swing.JLabel();
        currentObject = new javax.swing.JLabel();
        jMenuBar1 = new javax.swing.JMenuBar();
        jMenu1 = new javax.swing.JMenu();
        jMenu3 = new javax.swing.JMenu();
        jMenu2 = new javax.swing.JMenu();

        jInternalFrame1.setVisible(true);

        javax.swing.GroupLayout jInternalFrame1Layout = new javax.swing.GroupLayout(jInternalFrame1.getContentPane());
        jInternalFrame1.getContentPane().setLayout(jInternalFrame1Layout);
        jInternalFrame1Layout.setHorizontalGroup(
            jInternalFrame1Layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGap(0, 0, Short.MAX_VALUE)
        );
        jInternalFrame1Layout.setVerticalGroup(
            jInternalFrame1Layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGap(0, 0, Short.MAX_VALUE)
        );

//        setDefaultCloseOperation(javax.swing.WindowConstants.EXIT_ON_CLOSE);

        jLabel1.setText("Enter Cell IDs      :");

        cellIDs.addActionListener(new java.awt.event.ActionListener() {
            public void actionPerformed(java.awt.event.ActionEvent evt) {
                cellIDsActionPerformed(evt);
            }
        });
        cellIDs.addKeyListener(new java.awt.event.KeyAdapter() {
            public void keyPressed(java.awt.event.KeyEvent evt) {
                cellIDsKeyPressed(evt);
            }
            public void keyReleased(java.awt.event.KeyEvent evt) {
                cellIDsKeyReleased(evt);
            }
            public void keyTyped(java.awt.event.KeyEvent evt) {
                cellIDsKeyTyped(evt);
            }
        });

        submitButton.setText("Go");
        submitButton.setEnabled(false);
        submitButton.addActionListener(new java.awt.event.ActionListener() {
            public void actionPerformed(java.awt.event.ActionEvent evt) {
                submitButtonActionPerformed(evt);
            }
        });

        jLabel2.setText("(Or) Select a Cell ID  : ");

        cellID.addActionListener(new java.awt.event.ActionListener() {
            public void actionPerformed(java.awt.event.ActionEvent evt) {
                cellIDActionPerformed(evt);
            }
        });

        mainPanel.setMaximumSize(new java.awt.Dimension(700, 700));
        mainPanel.setPreferredSize(new java.awt.Dimension(600, 600));
        mainPanel.addFocusListener(new java.awt.event.FocusAdapter() {
            public void focusGained(java.awt.event.FocusEvent evt) {
                mainPanelFocusGained(evt);
            }
        });

        javax.swing.GroupLayout mainPanelLayout = new javax.swing.GroupLayout(mainPanel);
        mainPanel.setLayout(mainPanelLayout);
        mainPanelLayout.setHorizontalGroup(
            mainPanelLayout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGap(0, 927, Short.MAX_VALUE)
        );
        mainPanelLayout.setVerticalGroup(
            mainPanelLayout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGap(0, 593, Short.MAX_VALUE)
        );

        jLabel3.setText("3D Object currently on Canvas : ");

        jMenu1.setText("File");

        jMenu3.setText("Clear");
        jMenu1.add(jMenu3);

        jMenuBar1.add(jMenu1);

        jMenu2.setText("Reset");
        jMenuBar1.add(jMenu2);

        setJMenuBar(jMenuBar1);

        javax.swing.GroupLayout layout = new javax.swing.GroupLayout(getContentPane());
        getContentPane().setLayout(layout);
        layout.setHorizontalGroup(
            layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGroup(layout.createSequentialGroup()
                .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
                    .addGroup(layout.createSequentialGroup()
                        .addContainerGap()
                        .addComponent(jLabel2))
                    .addGroup(layout.createSequentialGroup()
                        .addGap(28, 28, 28)
                        .addComponent(jLabel1)))
                .addGap(22, 22, 22)
                .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
                    .addComponent(cellID, javax.swing.GroupLayout.PREFERRED_SIZE, 205, javax.swing.GroupLayout.PREFERRED_SIZE)
                    .addComponent(cellIDs, javax.swing.GroupLayout.PREFERRED_SIZE, 205, javax.swing.GroupLayout.PREFERRED_SIZE))
                .addGap(18, 18, 18)
                .addComponent(submitButton, javax.swing.GroupLayout.PREFERRED_SIZE, 53, javax.swing.GroupLayout.PREFERRED_SIZE)
                .addGap(53, 53, 53)
                .addComponent(jLabel3)
                .addPreferredGap(javax.swing.LayoutStyle.ComponentPlacement.UNRELATED)
                .addComponent(currentObject)
                .addGap(290, 290, 290))
            .addComponent(mainPanel, javax.swing.GroupLayout.DEFAULT_SIZE, 927, Short.MAX_VALUE)
        );
        layout.setVerticalGroup(
            layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
            .addGroup(layout.createSequentialGroup()
                .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.LEADING)
                    .addGroup(layout.createSequentialGroup()
                        .addContainerGap()
                        .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.BASELINE)
                            .addComponent(jLabel1)
                            .addComponent(cellIDs, javax.swing.GroupLayout.PREFERRED_SIZE, 20, javax.swing.GroupLayout.PREFERRED_SIZE))
                        .addPreferredGap(javax.swing.LayoutStyle.ComponentPlacement.UNRELATED)
                        .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.BASELINE)
                            .addComponent(jLabel2)
                            .addComponent(cellID, javax.swing.GroupLayout.PREFERRED_SIZE, javax.swing.GroupLayout.DEFAULT_SIZE, javax.swing.GroupLayout.PREFERRED_SIZE)))
                    .addGroup(layout.createSequentialGroup()
                        .addGap(19, 19, 19)
                        .addGroup(layout.createParallelGroup(javax.swing.GroupLayout.Alignment.BASELINE)
                            .addComponent(jLabel3)
                            .addComponent(currentObject)
                            .addComponent(submitButton, javax.swing.GroupLayout.PREFERRED_SIZE, 33, javax.swing.GroupLayout.PREFERRED_SIZE))))
                .addGap(18, 18, 18)
                .addComponent(mainPanel, javax.swing.GroupLayout.DEFAULT_SIZE, 593, Short.MAX_VALUE)
                .addContainerGap())
        );

//        pack();
    }/// </editor-fold>

    private void submitButtonActionPerformed(java.awt.event.ActionEvent evt) {
        // TODO add your handling code here:

        updateLabel();

    }

    private void cellIDsActionPerformed(java.awt.event.ActionEvent evt) {
        // TODO add your handling code here:
    }

    private void mainPanelFocusGained(java.awt.event.FocusEvent evt) {
        // TODO add your handling code here:
    }

    private void cellIDsKeyPressed(java.awt.event.KeyEvent evt) {
        // TODO add your handling code here:
        updateSubmitButton();
    }

    private void cellIDsKeyReleased(java.awt.event.KeyEvent evt) {
        // TODO add your handling code here:
        updateSubmitButton();
    }

    private void cellIDsKeyTyped(java.awt.event.KeyEvent evt) {
        // TODO add your handling code here:
        updateSubmitButton();
    }

    private void cellIDActionPerformed(java.awt.event.ActionEvent evt) {
        // TODO add your handling code here:

        updateSubmitButton();
    }

    public void updateSubmitButton()
    {
        if(cellID.getSelectedIndex() != 0)
        {
            cellIDs.setEnabled(false);
            submitButton.setEnabled(true);

            cells = cellID.getSelectedItem().toString().trim().split("-")[1].trim();
        }
        else
        {
            cellIDs.setEnabled(true);
            if(cellIDs.getText().length() > 0)
                submitButton.setEnabled(true);
            else
                submitButton.setEnabled(false);

            cells = cellIDs.getText();
        }


    }

    public void updateLabel()
    {

        currentObject.setText("");
        //logic for post selection

        String[] cellList = cells.toString().trim().split(" ");

        if(scenes.size() > 0)
        {
            for(BranchGroup root: scenes)
            {
                root.detach();
            }
        }

        scenes.clear();
        for(String cell : cellList)
        {
            cells = cell;
            objectsLoading();
            currentObject.setText(currentObject.getText()+" "+ cell);
        }


    }
    /**
    * @param args the command line arguments
    */

    public URL webrequest;

    // Variables declaration - do not modify
    private javax.swing.JComboBox cellID;
    private javax.swing.JTextField cellIDs;
    private javax.swing.JLabel currentObject;
    private javax.swing.JInternalFrame jInternalFrame1;
    private javax.swing.JLabel jLabel1;
    private javax.swing.JLabel jLabel2;
    private javax.swing.JLabel jLabel3;
    private javax.swing.JMenu jMenu1;
    private javax.swing.JMenu jMenu2;
    private javax.swing.JMenu jMenu3;
    private javax.swing.JMenuBar jMenuBar1;
    private javax.swing.JSeparator jSeparator1;
    private javax.swing.JPanel mainPanel;
    private javax.swing.JButton submitButton;
    // End of variables declaration


}
