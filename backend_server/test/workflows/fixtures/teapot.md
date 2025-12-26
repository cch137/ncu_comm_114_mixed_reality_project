```javascript
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';

/**
 * Procedural 3D Teapot Generator
 * Focuses on traditional "Yixing" style clay teapot (Zisha)
 * Uses LatheGeometry for the body and lid, and TubeGeometry for spout and handle.
 */

try {
    const scene = new THREE.Scene();

    // --- HELPER FUNCTIONS ---

    // Simple pseudo-random for procedural variations
    const seed = 12345;
    const random = (s) => {
        const x = Math.sin(s) * 10000;
        return x - Math.floor(x);
    };

    // Apply vertex colors based on height and some noise
    const applyClayColors = (geometry, baseColor, variation = 0.1) => {
        const position = geometry.attributes.position;
        const colors = [];
        const color = new THREE.Color();

        for (let i = 0; i < position.count; i++) {
            const y = position.getY(i);
            const noise = (random(i + seed) - 0.5) * variation;
            
            // Darker at the bottom, slightly lighter at the top
            const factor = 0.8 + (y * 0.2) + noise;
            color.copy(baseColor).multiplyScalar(factor);
            colors.push(color.r, color.g, color.b);
        }
        geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    };

    // --- MATERIALS ---
    // Traditional Purple Clay (Zisha) look: Deep brownish-red
    const clayBaseColor = new THREE.Color(0x63352d); 
    const clayMaterial = new THREE.MeshStandardMaterial({
        vertexColors: true,
        roughness: 0.7,
        metalness: 0.1,
    });

    // --- GEOMETRY ---

    // 1. Body (Lathe Geometry)
    const bodyPoints = [];
    for (let i = 0; i <= 10; i++) {
        const t = i / 10;
        // Profile curve for a classic bulbous teapot body
        const x = Math.sin(t * Math.PI) * 0.8 + 0.1 * Math.pow(t, 2);
        const y = (t - 0.5) * 1.2;
        bodyPoints.push(new THREE.Vector2(x, y));
    }
    const bodyGeom = new THREE.LatheGeometry(bodyPoints, 32);
    applyClayColors(bodyGeom, clayBaseColor);
    const body = new THREE.Mesh(bodyGeom, clayMaterial);
    scene.add(body);

    // 2. Lid (Lathe Geometry)
    const lidPoints = [];
    lidPoints.push(new THREE.Vector2(0, 0.1));
    lidPoints.push(new THREE.Vector2(0.3, 0.12));
    lidPoints.push(new THREE.Vector2(0.45, 0.05));
    lidPoints.push(new THREE.Vector2(0.48, 0));
    const lidGeom = new THREE.LatheGeometry(lidPoints, 32);
    applyClayColors(lidGeom, clayBaseColor);
    const lid = new THREE.Mesh(lidGeom, clayMaterial);
    lid.position.y = 0.65;
    scene.add(lid);

    // 2b. Lid Knob (Small Sphere)
    const knobGeom = new THREE.SphereGeometry(0.08, 12, 12);
    applyClayColors(knobGeom, clayBaseColor);
    const knob = new THREE.Mesh(knobGeom, clayMaterial);
    knob.position.y = 0.8;
    scene.add(knob);

    // 3. Spout (Tube Geometry)
    const spoutCurve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(0.5, -0.1, 0),
        new THREE.Vector3(0.9, 0.1, 0),
        new THREE.Vector3(1.1, 0.5, 0),
        new THREE.Vector3(1.2, 0.6, 0),
    ]);
    const spoutGeom = new THREE.TubeGeometry(spoutCurve, 20, 0.08, 12, false);
    applyClayColors(spoutGeom, clayBaseColor);
    const spout = new THREE.Mesh(spoutGeom, clayMaterial);
    scene.add(spout);

    // 4. Handle (Tube Geometry)
    const handleCurve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(-0.5, 0.4, 0),
        new THREE.Vector3(-1.0, 0.6, 0),
        new THREE.Vector3(-1.2, 0.0, 0),
        new THREE.Vector3(-0.7, -0.4, 0),
    ]);
    const handleGeom = new THREE.TubeGeometry(handleCurve, 20, 0.06, 12, false);
    applyClayColors(handleGeom, clayBaseColor);
    const handle = new THREE.Mesh(handleGeom, clayMaterial);
    scene.add(handle);

    // 5. Base Ring
    const basePoints = [
        new THREE.Vector2(0.4, -0.1),
        new THREE.Vector2(0.5, 0),
        new THREE.Vector2(0.4, 0.05)
    ];
    const baseGeom = new THREE.LatheGeometry(basePoints, 32);
    applyClayColors(baseGeom, clayBaseColor);
    const base = new THREE.Mesh(baseGeom, clayMaterial);
    base.position.y = -0.65;
    scene.add(base);

    // --- LIGHTING (for GLTF previewers) ---
    const directLight = new THREE.DirectionalLight(0xffffff, 1);
    directLight.position.set(5, 10, 7);
    scene.add(directLight);
    const ambientLight = new THREE.AmbientLight(0x404040, 2);
    scene.add(ambientLight);

    // --- EXPORT ---
    const exporter = new GLTFExporter();
    exporter.parse(
        scene,
        (result) => {
            if (typeof EXPORT_GLTF === 'function') {
                EXPORT_GLTF(result);
            }
        },
        (error) => {
            if (typeof EXPORT_ERROR === 'function') {
                EXPORT_ERROR(error);
            }
        },
        { 
            binary: false, 
            embedImages: false,
            animations: [],
            truncateDrawRange: true
        }
    );

} catch (err) {
    if (typeof EXPORT_ERROR === 'function') {
        EXPORT_ERROR(err.message);
    }
}
```