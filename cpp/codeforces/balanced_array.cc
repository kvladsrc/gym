#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    if ((n / 2) % 2) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";

    for (int i = 1; i <= n / 2; ++i) {
      std::cout << 2 * i << " ";
    }

    for (int i = 1; i <= n / 2; ++i) {
      if (i == n / 2) {
        std::cout << 2 * i - 1 + n / 2 << "\n";
        continue;
      }
      std::cout << 2 * i - 1 << " ";
    }
  }

  return 0;
}
