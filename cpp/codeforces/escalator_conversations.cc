#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
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
using std::priority_queue;
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
    int n, m, k, h;
    std::cin >> n >> m >> k >> h;
    int res = 0;

    while (n--) {
      int buf;
      std::cin >> buf;

      int diff = abs(buf - h);
      if (diff % k == 0 && diff < (k * m) && buf != h) {
        res++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
