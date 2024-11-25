#include <iostream>

int main(int argc, char *argv[]) {
  int k, n, w;
  std::cin >> k >> n >> w;
  ;
  int need = ((w + 1) * w / 2) * k;
  if (need > n)
    std::cout << need - n << "\n";
  else
    std::cout << 0 << "\n";

  return 0;
}
