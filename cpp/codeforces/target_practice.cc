#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int score(int i, int j) {
  int x = min(abs(i - 5), abs(i - 6));
  int y = min(abs(j - 5), abs(j - 6));
  int tmp = max(x, y);
  return 5 - tmp;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int res = 0;

    for (int i = 1; i <= 10; ++i) {
      for (int j = 1; j <= 10; ++j) {
        char c;
        std::cin >> c;
        if (c == 'X') {
          res += score(i, j);
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
