#include <iostream>

int main(int argc, char* argv[]) {
  int stops;
  std::cin >> stops;
  int max = 0;
  int cur = 0;
  while (stops) {
    stops--;
    int in_num, out_num;
    std::cin >> out_num >> in_num;
    cur += in_num;
    cur -= out_num;
    if (cur > max) {
      max = cur;
    }
  }
  std::cout << max << "\n";

  return 0;
}
