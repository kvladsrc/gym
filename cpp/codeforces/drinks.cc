#include <iostream>

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  double res = 0.0;
  for (int i = 0; i < n; ++i) {
    double perc;
    std::cin >> perc;
    res += perc / n;
  }
  std::cout << res << "\n";

  return 0;
}
