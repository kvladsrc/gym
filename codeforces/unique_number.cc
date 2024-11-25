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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x;
    std::cin >> x;

    int res = 0;
    int h = 9, power = 1;
    while (x) {
      if (h == 0) {
        res = -1;
        break;
      }

      res += min(h, x) * power;
      x -= min(h, x);
      power *= 10;
      h--;
    }

    std::cout << res << "\n";
  }

  return 0;
}
