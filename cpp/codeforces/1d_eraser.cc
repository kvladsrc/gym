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
    int n, k;
    std::cin >> n >> k;

    int rest;
    bool filling = false;
    int res = 0;
    while (n--) {
      char buf;
      std::cin >> buf;

      if (filling) {
        if (rest) {
          rest--;
          continue;
        }
      }

      if (buf == 'B') {
        res++;
        rest = k - 1;
        filling = true;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
