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

    string in;
    std::cin >> in;

    string want;
    std::cin >> want;

    int fill = 0, remove = 0;
    for (int i = 0; i < n; ++i) {
      if (in[i] != want[i]) {
        if (want[i] == '1') {
          fill++;
        } else {
          remove++;
        }
      }
    }

    int res = min(fill, remove);
    res = fill + remove - res;
    std::cout << res << "\n";
  }

  return 0;
}
