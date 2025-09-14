const formCadastro = document.querySelector(".register-card form");

formCadastro.addEventListener("submit", async (e) => {
  e.preventDefault();

  const fullName = document.getElementById("fullname").value.trim();
  const email = document.getElementById("email").value.trim();
  const password = document.getElementById("password").value;
  const confirmPassword = document.getElementById("confirm-password").value;

  if (!fullName || !email || !password || !confirmPassword) {
    alert("Por favor, preencha todos os campos.");
    return;
  }

  if (password !== confirmPassword) {
    alert("As senhas não coincidem.");
    return;
  }

  const data = { fullName, email, password };

  try {
    const res = await fetch("https://localhost:5001/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data)
    });

    const json = await res.json();

    if (!res.ok) throw new Error(json.error || "Erro ao registrar usuário.");

    alert(json.message);
    window.location.href = "login.html";
  } catch (err) {
    alert(err.message);
  }
});
