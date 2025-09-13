import { getUsers, getUserById } from "./apiController.js";

async function carregarUsuarios() {
  const usuarios = await getUsers();
  if (usuarios) {
    console.log("Usuários:", usuarios);
    document.getElementById("lista").innerHTML = usuarios
      .map(u => `<li>${u.name}</li>`)
      .join("");
  }
}

async function carregarUmUsuario() {
  const user = await getUserById(1);
  console.log("Usuário 1:", user);
}

carregarUsuarios();
carregarUmUsuario();
