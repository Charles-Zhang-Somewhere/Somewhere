0. (Design ) A separate implementation with intended purpose like ULC. Mostly (and as primary user) for SW, and planning visualization.
    * This is NOT voxel based, it's a blocking engine!
    * Major reference: migrating our CofN Blender outline into SW
1. (Design) The compiler only deals with one piece
1. (Design) Notice no longer do we use named entity and embedded comment
1. (Design) Because this is preview, scene, lighting, camera, materials should all be automatic
1. (Syntax) Action + Parameters
2. (Action) place
2. (Preprocessor Action) define (pure macro)
3. (Preprocessor Syntax Sugar) Befault action is just "place"
4. (Primitives) All basic primitives in original ULC, including `.obj` (considered a primitive)
5. (SW, previewer) For preview we have many options: 1) ThrreJS (webgl) 2) pbrt 3) Ulc 4) MagicaVoxel 5) Custom made specific format GameEngine 
6. (SW, acrion, previewer) `set parameter value` (e.g. preferred render output)
7. (SW, action, preview, preprocessor) `use filterforreceipe' but this is semantically (exactly) the same as "place", so let's just use "place" and figure it out in the previewer; Clearly, a stack will keep track of things and recursive dependency is not allowed.
8. (Design) Let this not be a separate program, to avoid management complexity - instead let "cityscript" be a specification.
	* Stages: Preprocessing, processor (generates output)
9. (Syntax, Action, SW, Previewer) `preview` instruction (on the source side just like set, but syntax like place with primitives) and `preview` action (on the user side just like place)
    * The implementation in SW will be in lua and procedural so it's flexible and extendable.