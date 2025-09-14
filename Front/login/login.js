const formLogin = document.querySelector(".login-card form");

formLogin.addEventListener("submit", async (e) => {
  e.preventDefault();

  const email = document.getElementById("username").value.trim();
  const password = document.getElementById("password").value;

  if (!email || !password) {
    alert("Preencha todos os campos.");
    return;
  }

  const data = { email, password };

  try {
    const res = await fetch("https://localhost:5001/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data)
    });

    const json = await res.json();

    if (!res.ok) throw new Error(json.error || "Credenciais inválidas.");

    // Salva token e informações do usuário no localStorage
    localStorage.setItem("token", json.token);
    localStorage.setItem("expiresAt", json.expiresAt);
    localStorage.setItem("email", json.email);
    localStorage.setItem("roles", JSON.stringify(json.roles));

    alert("Login realizado com sucesso!");
    window.location.href = "dashboard.html"; // redirecionar para a área logada
  } catch (err) {
    alert(err.message);
  }
});
