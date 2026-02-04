#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

void draw_the_snake(int n, int m) {
  for (int i = 1; i <= n; ++i) {
    for (int j = 1; j <= m; ++j) {
      if (!(i % 2)) {
        if (!(i % 4) && j == 1) {
          std::cout << "#";
          continue;
        }

        if ((i % 4) && j == m) {
          std::cout << "#";
          continue;
        }

        std::cout << ".";
        continue;
      } else {
        std::cout << "#";
      }
    }
    std::cout << "\n";
  }
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;
  draw_the_snake(n, m);
  return 0;
}
