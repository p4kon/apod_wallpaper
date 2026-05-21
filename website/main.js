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

const lightbox = document.querySelector("#mediaLightbox");
const lightboxImage = document.querySelector("#lightboxImage");
const lightboxCaption = document.querySelector("#lightboxCaption");
const lightboxCloseButtons = document.querySelectorAll(".lightbox-backdrop, .lightbox-close");

function closeLightbox() {
  if (!lightbox || !lightboxImage || !lightboxCaption) {
    return;
  }

  lightbox.hidden = true;
  lightboxImage.src = "";
  lightboxImage.alt = "";
  lightboxCaption.textContent = "";
  document.body.classList.remove("has-lightbox");
}

document.querySelectorAll("[data-lightbox]").forEach((trigger) => {
  trigger.addEventListener("click", () => {
    if (!lightbox || !lightboxImage || !lightboxCaption) {
      return;
    }

    const source = trigger.getAttribute("data-lightbox");
    const title = trigger.getAttribute("data-title") || trigger.querySelector("img")?.alt || "";

    lightboxImage.src = source;
    lightboxImage.alt = title;
    lightboxCaption.textContent = title;
    lightbox.hidden = false;
    document.body.classList.add("has-lightbox");
  });
});

lightboxCloseButtons.forEach((button) => {
  button.addEventListener("click", closeLightbox);
});

window.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && lightbox && !lightbox.hidden) {
    closeLightbox();
  }
});
