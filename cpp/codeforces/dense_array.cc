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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int res = 0, pre;
    std::cin >> pre;
    n--;

    while (n--) {
      int buf;
      std::cin >> buf;

      int big = max(buf, pre);
      int small = min(buf, pre);

      while (small * 2 < big) {
        small *= 2;
        res++;
      }

      pre = buf;
    }

    std::cout << res << "\n";
  }

  return 0;
}
