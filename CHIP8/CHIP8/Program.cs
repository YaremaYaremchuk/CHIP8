
using SDL2;

using System.Text;
using System.IO;
using System.Drawing;
using System.Reflection.PortableExecutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{


    
    

    public static void Main()
    {




        if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
        {
            Console.WriteLine("SDL FAILURE");
            return;
        }

        nint window = SDL.SDL_CreateWindow("CHIP8", 256, 256, 64 * 8, 32 * 8, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        if (window == nint.Zero)
        {
            Console.WriteLine("No window");
            return;
        }

        nint renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if(renderer == nint.Zero)
        {
            Console.WriteLine("No renderer");
            return;
        }


        CPU cpu = new CPU();
        SDL.SDL_Event sdl_event;
        uint[] bytes = new uint[64 * 32];

        nint sdl_surface, texture = nint.Zero;
        int[] codes = new int[16] {
            120,
            49,
            50,
            51,
            113,
            119,
            101,
            97,
            100,
            115,
            122,
            99,
            52,
            114,
            102,
            118};


        Stopwatch frame = Stopwatch.StartNew();
        

        int cursamples = 0;
        int ticksamples = 0;
        SDL.SDL_AudioSpec audiospec = new SDL.SDL_AudioSpec();
        audiospec.freq = 44100;
        audiospec.channels = 1;
        audiospec.samples = 256;
        audiospec.format = SDL.AUDIO_S8;
        audiospec.callback = new SDL.SDL_AudioCallback((userdata, stream, length) =>
        {
            if (cpu == null) return;
            sbyte[] wavedt = new sbyte[length];
            for (int i = 0; i < wavedt.Length && cpu.audio_timer > 0; i++, ticksamples++)
            {
                if(ticksamples == 730)
                {
                    ticksamples = 0;
                    cpu.audio_timer--;
                }
                wavedt[i] = (sbyte)(127 * Math.Sin(cursamples * Math.PI * 2 * 604.1 / 44100));
                cursamples++;
            }
            byte[] bytedata = (byte[])(Array)wavedt;
            Marshal.Copy(bytedata, 0, stream, bytedata.Length);
        }
        );

        SDL.SDL_OpenAudio(ref audiospec, nint.Zero);
        SDL.SDL_PauseAudio(0);

        bool running = true;
        while (running)
        {
            cpu.Run();
            
            

            

            if(frame.ElapsedMilliseconds > 16)
            {
                while (SDL.SDL_PollEvent(out sdl_event) != 0)
                {
                    if (sdl_event.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        running = false;
                    }
                    else if (sdl_event.type == SDL.SDL_EventType.SDL_KEYDOWN)
                    {

                        for (int i = 0; i < 16; i++)
                        {
                            if (codes[i] == (int)sdl_event.key.keysym.sym)
                            {
                                cpu.keyboard[i] = 1;
                            }
                        }

                    }
                    else if (sdl_event.type == SDL.SDL_EventType.SDL_KEYUP)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            if (codes[i] == (int)sdl_event.key.keysym.sym)
                            {
                                cpu.keyboard[i] = 0;
                            }
                        }
                    }
                }

                for (int i = 0; i < bytes.Length; i++)
                {
                    if (cpu.graphics[i] == 1)
                    {
                        bytes[i] = 0xffffffff;
                    }

                    else
                    {
                        bytes[i] = 0;
                    }

                }
                var displayHandler = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                if (texture != nint.Zero)
                {
                    SDL.SDL_DestroyTexture(texture);
                }
                sdl_surface = SDL.SDL_CreateRGBSurfaceFrom(displayHandler.AddrOfPinnedObject(), 64, 32, 32, 64 * 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
                texture = SDL.SDL_CreateTextureFromSurface(renderer, sdl_surface);

                displayHandler.Free();

                SDL.SDL_RenderClear(renderer);
                SDL.SDL_RenderCopy(renderer, texture, nint.Zero, nint.Zero);
                SDL.SDL_RenderPresent(renderer);

                frame.Restart();
            }

            
            Thread.Sleep(1);

        }

        SDL.SDL_DestroyRenderer(renderer);
        SDL.SDL_DestroyWindow(window);


    }
}





public class CPU
{
    public Random rand = new Random();

    public ushort opcode;
    public byte[] memory = new byte[4096];
    public byte[] graphics = new byte[64 * 32];
    public byte[] registers = new byte[16];
    public ushort index;
    public ushort program_counter;

    public byte delay_timer;
    public byte audio_timer;


    public ushort[] stack = new ushort[16];
    public ushort stackp;

    public byte[] keyboard = new byte[16];

    public readonly byte[] chip8_fontset = new byte[] {
  0xF0, 0x90, 0x90, 0x90, 0xF0, //0
  0x20, 0x60, 0x20, 0x20, 0x70, // 1
  0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
  0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
  0x90, 0x90, 0xF0, 0x10, 0x10, // 4
  0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
  0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
  0xF0, 0x10, 0x20, 0x40, 0x40, // 7
  0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
  0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
  0xF0, 0x90, 0xF0, 0x90, 0x90, // A
  0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
  0xF0, 0x80, 0x80, 0x80, 0xF0, // C
  0xE0, 0x90, 0x90, 0x90, 0xE0, // D
  0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
  0xF0, 0x80, 0xF0, 0x80, 0x80  // F
};

    public CPU()
    {
        program_counter = 0x200;
        opcode = 0;
        index = 0;
        stackp = 0;
        delay_timer = 0;
        audio_timer = 0;

        for(int i=0; i<chip8_fontset.Length; i++)
        {
            memory[i] = chip8_fontset[i];
        }

        using (BinaryReader reader = new BinaryReader(new FileStream("Pong (1 player).ch8", FileMode.Open)))
        {

            while(reader.BaseStream.Position < reader.BaseStream.Length)
            {
                memory[reader.BaseStream.Position + 0x200] = reader.ReadByte();
            }
        }

    }

    public void IncrementPC()
    {
        program_counter += 2;
    }

    private Stopwatch clock = new Stopwatch();


    public void Run()
    {
        if (!clock.IsRunning) clock.Start();
        if(clock.ElapsedMilliseconds > 16)
        {
            if (delay_timer > 0) delay_timer--;
            if (audio_timer > 0) audio_timer--;
            clock.Restart();
        }
        opcode = (ushort)(memory[program_counter] << 8 | memory[program_counter + 1]);
        var first = (ushort)opcode >> 12;


        switch (first)
        {
            case (0x0):
                {

                    if (opcode == 0x00E0)
                    {
                        for (int i = 0; i < graphics.Length; i++)
                        {
                            graphics[i] = 0;
                        }
                    }

                    else if (opcode == 0x00EE)
                    {
                        stackp--;
                        program_counter = stack[stackp];
                    }
                    IncrementPC();

                    break;
                }


            case (0x1):
                {
                    program_counter = (ushort)(opcode & 0x0FFF);
                    break;
                }


            case (0x2):
                {
                    stack[stackp] = program_counter;
                    stackp++;
                    program_counter = (ushort)(opcode & 0x0FFF);


                    break;
                }
            case (0x3):
                {

                    var x = ((opcode & 0x0F00) >> 8);

                    if (registers[x] == (opcode & 0x00FF))
                    {
                        IncrementPC();

                    }


                    IncrementPC();

                    break;
                }

            case (0x4):
                {
                    var x = ((opcode & 0x0F00) >> 8);

                    if (registers[x] != (opcode & 0x00FF))
                    {
                        IncrementPC();

                    }


                    IncrementPC();

                    break;
                }

            case (0x5):
                {
                    var x = ((opcode & 0x0F00) >> 8);
                    var y = ((opcode & 0x00F0) >> 4);
                    if (registers[x] == registers[y])
                    {
                        IncrementPC();

                    }


                    IncrementPC();

                    break;
                }

            case (0x9):
                {

                    var x = ((opcode & 0x0F00) >> 8);
                    var y = ((opcode & 0x00F0) >> 4);
                    if (registers[x] != registers[y])
                    {
                        IncrementPC();

                    }


                    IncrementPC();

                    break;
                }

            case (0x6):
                {

                    var x = ((opcode & 0x0F00) >> 8);
                    var kk = (byte)((opcode & 0x00FF));
                    registers[x] = kk;
                    IncrementPC();

                    break;
                }


            case (0x7):
                {
                    unchecked
                    {
                        var x = ((opcode & 0x0F00) >> 8);
                        var kk = (byte)((opcode & 0x00FF));
                        registers[x] += kk;
                        IncrementPC();

                        break;
                    }


                }
            case (0x8):
                {
                    var x = ((opcode & 0x0F00) >> 8);
                    var y = ((opcode & 0x00F0) >> 4);
                    var m = opcode & 0x000F;

                    switch (m)
                    {
                        case (0):
                            {
                                registers[x] = registers[y];
                                break;
                            }
                        case (1):
                            {
                                registers[x] |= registers[y];
                                break;
                            }
                        case (2):
                            {
                                registers[x] &= registers[y];
                                break;
                            }
                        case (3):
                            {
                                registers[x] ^= registers[y];
                                break;
                            }
                        case (4):
                            {
                                unchecked
                                {
                                    var sum = (ushort)(registers[x]);
                                    sum += registers[y];

                                    if (sum > 255) registers[0xF] = 1;
                                    else registers[0xF] = 0;


                                    registers[x] = (byte)(sum & 0x00FF);

                                    break;

                                }

                            }
                        case (5):
                            {
                                unchecked
                                {

                                    if (registers[x] > registers[y]) registers[0xF] = 1;
                                    else registers[0xF] = 0;


                                    registers[x] -= registers[y];

                                    break;

                                }

                            }


                        case (6):
                            {


                                registers[0xF] = ((byte)(registers[x] & 1));
                                registers[x] = (byte)(registers[x] >> 1);

                                break;



                            }

                        case (7):
                            {
                                unchecked
                                {

                                    if (registers[y] > registers[x]) registers[0xF] = 1;
                                    else registers[0xF] = 0;


                                    registers[x] = (byte)(registers[y] - registers[x]);

                                    break;

                                }

                            }

                        case (14):
                            {


                                if ((registers[x] & 0x80) != 0) registers[0xF] = 1;
                                else
                                {
                                    registers[0xF] = 0;
                                }
                                registers[x] = (byte)(registers[x] << 1);

                                break;



                            }




                    }


                    IncrementPC();
                    break;

                }


            case (0xA):
                {

                    index = (ushort)(opcode & 0x0FFF);



                    IncrementPC();
                    break;

                }

            case (0xB):
                {

                    program_counter = (ushort)((opcode & 0x0FFF) + registers[0]);





                    break;

                }



            case (0xC):
                {

                    var x = ((opcode & 0x0F00) >> 8);
                    var kk = opcode & 0x00FF;
                    unchecked
                    {
                        registers[x] = (byte)((uint)(rand.Next(0, 256) & kk));

                    }
                    IncrementPC();


                    break;

                }

            case (0xD):
                {

                    registers[0xF] = 0;
                    var xx = ((opcode & 0x0F00) >> 8);
                    var yy = ((opcode & 0x00F0) >> 4);
                    var nn = (opcode & 0x000F);

                    var regx = registers[xx];
                    var regy = registers[yy];

                    for (int y = 0; y < nn; y++)
                    {
                        var pixel = memory[index + y];

                        for (int x = 0; x < 8; x++)
                        {

                            var spritePixel = (pixel & (1 << 7 - x)) != 0;

                            if (spritePixel)
                            {
                                var tx = (regx + x) % 64;
                                var ty = (regy + y) % 32;

                                var idx = tx + ty * 64;
                                graphics[idx] ^= 1;

                                if (graphics[idx] == 0)
                                {
                                    registers[0xF] = 1;
                                }
                            }
                        }
                    }

                    IncrementPC();



                    break;
                }

            case (0xE):
                {
                    var x = ((opcode & 0x0F00) >> 8);

                    var kk = opcode & 0x00FF;

                    if (kk == 0xA1)
                    {
                        if (keyboard[registers[x]] != 1)
                        {
                            IncrementPC();
                        }
                    }
                    else if (kk == 0x9E)
                    {
                        if (keyboard[registers[x]] == 1)
                        {
                            IncrementPC();
                        }
                    }

                    IncrementPC();


                    break;
                }


            case (0xF):
                {

                    var x = ((opcode & 0x0F00) >> 8);

                    var kk = opcode & 0x00FF;

                    if (kk == 0x07)
                    {
                        registers[x] = delay_timer;

                    }

                    else if (kk == 0x0A)
                    {
                        var key_pressed = false;

                        for (int i = 0; i < keyboard.Length; i++)
                        {
                            if (keyboard[i] != 0)
                            {
                                unchecked
                                {
                                    registers[x] = (byte)i;
                                }

                                key_pressed = true;
                                break;
                            }
                        }

                        if (!key_pressed)
                        {
                            return;
                        }
                    }

                    else if (kk == 0x15)
                    {
                        delay_timer = registers[x];
                    }

                    else if (kk == 0x18)
                    {
                        audio_timer = registers[x];
                    }
                    else if (kk == 0x1E)
                    {
                        index += registers[x];

                    }

                    else if (kk == 0x29)
                    {
                        if (registers[x] < 16)
                        {
                            index = (ushort)(registers[x] * 0x5);
                        }

                    }


                    else if (kk == 0x33)
                    {
                        memory[index] = ((byte)(registers[x] / 100));
                        memory[index + 1] = (byte)((registers[x] / 10) % 10);

                        memory[index + 2] = (byte)(registers[x] % 10);

                    }

                    else if (kk == 0x55)
                    {

                        for (int counter = 0; counter <= x; counter++)
                        {
                            memory[index + counter] = registers[counter];
                        }
                    }

                    else if (kk == 0x65)
                    {
                        for (int counter = 0; counter <= x; counter++)
                        {
                            registers[counter] = memory[index + counter];
                        }
                    }

                    IncrementPC();
                    break;

                }



            default:
                Console.WriteLine("Pizda");
                break;




        }


    }
}