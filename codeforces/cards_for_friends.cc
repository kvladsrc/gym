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
    int w, h, n;
    std::cin >> w >> h >> n;
    int pieces = 1;

    while (w % 2 == 0) {
      w /= 2;
      pieces *= 2;
    }

    while (h % 2 == 0) {
      h /= 2;
      pieces *= 2;
    }

    if (pieces >= n) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }
  return 0;
}
