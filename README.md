# Memory Reader - Cave Bot com Overlay de Grid 5x5

## 📖 Sobre o Projeto

Este é um projeto educacional desenvolvido em C# que demonstra técnicas de leitura de memória de processos externos e criação de overlays transparentes. O software monitora a posição de jogadores em um jogo específico (OTP) e exibe visualmente essas informações em uma grade 5x5.

**⚠️ AVISO: Este projeto tem fins exclusivamente educacionais e de aprendizado. Não deve ser usado para cheating ou atividades maliciosas.**

## 🎯 Funcionalidades Principais

- **Leitura de Memória**: Acesso e leitura de memória de processos externos
- **Overlay Transparente**: Interface gráfica sobreposta ao jogo
- **Grid 5x5**: Visualização da posição do jogador e outros players próximos
- **Cache Inteligente**: Sistema de cache para otimizar as leituras de memória
- **Monitoramento em Tempo Real**: Atualização contínua das posições

## 🏗️ Arquitetura do Projeto

### Estrutura de Classes Principais

#### `Form1` - Classe Principal
- Gerencia a interface do usuário
- Controla o processo de leitura de memória
- Implementa o sistema de cache

#### `PlayerData` - Estrutura de Dados do Jogador
```csharp
public class PlayerData
{
    public string Name { get; set; }
    public ushort X { get; set; }
    public ushort Y { get; set; }
    public byte Z { get; set; }
    public byte Direction { get; set; }
    public bool IsLocalPlayer { get; set; }
    // ... endereços de memória e status
}
```

#### `OverlayForm` - Formulário de Overlay
- Renderiza a grade 5x5 com posições
- Interface transparente e sempre visível
- Adaptável ao tamanho da janela do jogo

## 🔧 Tecnologias Utilizadas

- **.NET Framework** - Plataforma de desenvolvimento
- **Windows API** - Funções nativas para manipulação de processos e janelas
- **GDI+** - Renderização gráfica do overlay
- **Multithreading** - Timer para monitoramento assíncrono

## 🚀 Como Funciona

### 1. Acesso ao Processo
```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);
```

### 2. Leitura de Memória
```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
```

### 3. Sistema de Cache
- Cache de endereços de memória por 5 minutos
- Scan completo apenas quando necessário
- Otimização de performance

### 4. Overlay
```csharp
int extendedStyle = GetWindowLong(this.Handle, -20);
SetWindowLong(this.Handle, -20, extendedStyle | 0x80000 | 0x20);
```

## 📊 Estrutura da Grade 5x5

A overlay exibe:
- **Posição Central**: Jogador monitorado (amarelo)
- **Posições Relativas**: Outros jogadores próximos (ciano)
- **Coordenadas**: X, Y, Z de cada tile
- **Direção**: Setas indicando a orientação do jogador
- **Informações Detalhadas**: Nome, coordenadas, timestamp

## 🛡️ Considerações de Segurança

- **Uso Educacional**: Desenvolvido para fins de aprendizado
- **Administrador**: Pode requerer elevação de privilégios
- **Antivirus**: Pode ser detectado como falso positivo devido às técnicas usadas

## 📝 Conceitos Educacionais Abordados

1. **API do Windows**: Uso de funções nativas
2. **Gerenciamento de Memória**: Leitura de processos externos
3. **Programação Gráfica**: Renderização com GDI+
4. **Overlay Applications**: Janelas transparentes e sempre no topo
5. **Pattern Matching**: Busca de padrões na memória
6. **Cache Systems**: Otimização de performance
7. **Multithreading**: Operações assíncronas com timers

## 🔄 Fluxo de Operação

1. **Attach ao Processo**: Localiza e conecta ao processo do jogo
2. **Busca de Players**: Procura por nomes de jogadores na memória
3. **Leitura de Dados**: Extrai coordenadas e informações dos players
4. **Renderização**: Desenha a grade com as posições atualizadas
5. **Atualização Contínua**: Monitora mudanças em tempo real

## 💡 Aprendizados Principais

Este projeto demonstra:
- Técnicas avançadas de interprocess communication
- Manipulação de janelas e overlays
- Otimização de leituras de memória
- Renderização gráfica eficiente
- Gerenciamento de recursos do sistema

## 📄 Licença

Este projeto é disponibilizado para fins educacionais. O uso para qualquer finalidade que viole os termos de serviço de jogos ou atividades maliciosas é expressamente proibido.

---

**Desenvolvido para fins educacionais - Compreensão de técnicas de programação avançada**
