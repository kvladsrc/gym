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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, even = 0, odd = 0;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf % 2 == i % 2) continue;

      if (buf % 2) {
        odd++;
      } else {
        even++;
      }
    }

    if (even || odd) {
      if (even != odd) {
        std::cout << -1 << "\n";
      } else {
        std::cout << even << "\n";
      }
    } else {
      std::cout << 0 << "\n";
    }
  }
  return 0;
}
