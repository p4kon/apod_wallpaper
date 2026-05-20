const preview = document.querySelector(".app-preview");

if (preview && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
  window.addEventListener("mousemove", (event) => {
    const x = (event.clientX / window.innerWidth - 0.5) * 8;
    const y = (event.clientY / window.innerHeight - 0.5) * -6;
    preview.style.transform = `perspective(900px) rotateY(${x - 5}deg) rotateX(${y + 4}deg)`;
  });

  window.addEventListener("mouseleave", () => {
    preview.style.transform = "perspective(900px) rotateY(-5deg) rotateX(4deg)";
  });
}
