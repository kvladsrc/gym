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
    int n;
    std::cin >> n;

    int64_t res = 0;
    bool in = false;
    int operations = 0;
    while (n--) {
      int64_t buf;
      std::cin >> buf;

      if (buf < 0 && !in) {
        in = true;
        operations++;
      } else if (in && buf > 0) {
        in = false;
      }

      res += abs(buf);
    }

    std::cout << res << " ";
    std::cout << operations << "\n";
  }

  return 0;
}
