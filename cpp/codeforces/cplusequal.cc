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
    int64_t a, b, n;
    std::cin >> a >> b >> n;

    int64_t operations = 0;
    bool a_turn = a < b;
    while (a <= n && b <= n) {
      if (a_turn) {
        a += b;
      } else {
        b += a;
      }
      a_turn = !a_turn;
      operations++;
    }

    std::cout << operations << "\n";
  }
  return 0;
}
