# README header artwork

`lunchmoney-sdk-header.png` is the generated header used by the repository README.
It was created with the built-in image generation tool and is stored locally so
GitHub can load it from the repository.

## References

- [.NET brand repository](https://github.com/dotnet/brand):
  [original logo](https://raw.githubusercontent.com/dotnet/brand/main/logo/dotnet-logo.png),
  saved as `references/dotnet-logo.png`.
- [Lunch Money](https://lunchmoney.app/):
  [original mascot](https://lunchmoney.app/assets/images/logos/mascot.png),
  saved as `references/lunchmoney-mascot.png`.
- Layout inspiration: [Refit](https://github.com/reactiveui/refit) and
  [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet).

## Generation prompt

```text
Use case: logo-brand. Asset type: polished wide GitHub README header banner for a .NET SDK repository, approximately 3:1 aspect ratio (1800 x 600 composition). Primary request: create a beautiful, restrained editorial banner combining the official .NET logo and the Lunch Money mascot, with the project name. Input image 1 is the official .NET logo reference: preserve its exact white .NET lettering and purple square. Input image 2 is the official Lunch Money mascot reference: preserve the recognizable yellow smiling coin with dark outline, white highlights and rosy cheeks. Keep both marks faithful and clearly separate, with original proportions and no redesign. Background: warm ivory/off-white, subtle fine paper texture, ample negative space, soft lavender and pale gold accent arcs, a few very fine curved connection lines suggesting integration. Composition: .NET logo placed on the left as a neat flat purple square, the mascot on the right at a similar visual size, a strong elegant central typographic lockup. Large dark ink sans-serif headline, exact text: "LunchMoney.Sdk". Smaller exact subtitle below it: "A typed .NET client for Lunch Money". Typography impeccably spaced, perfectly readable at 900px wide, modern developer-tool aesthetic, balanced margins. Logos occupy no more than a quarter of the banner width each. Palette: official .NET purple #512BD4, warm coin yellow, deep ink and warm ivory. Flat crisp graphic design, subtle dimensional shadows only around logo motifs, no mockup device, no dashboard, no extraneous icons, no gradients overwhelming text, no sponsor or endorsement language, no badges inside the image, no watermarks. All text and both logos fully visible with generous safe padding.
```

## Refinement prompt

```text
Edit the first image (the README header banner). Keep its exact 3:1 composition, ivory paper background, all typography, the title "LunchMoney.Sdk", the subtitle "A typed .NET client for Lunch Money", and decorative background arcs. Correct ONLY the two brand marks to match the supplied references verbatim. Image 2 is the official .NET logo: use the exact original white lettering and solid purple square without gradient, perspective or shadow. Image 3 is the official Lunch Money mascot: place THIS exact flat original coin graphic, preserving its asymmetric contour, facial details, pale highlight, rosy cheeks, colors, tilt and proportions, instead of the current redrawn coin. Treat both reference marks as immutable graphic inserts: no redesign, no repaint, no extra rings, no different eyes or mouth, no 3D rendering, no cast shadows, no changes to brand shapes. Keep generous clear space. Preserve all remaining banner elements and text unchanged.
```
