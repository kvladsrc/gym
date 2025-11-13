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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string in;
    std::cin >> in;

    int minus = 0, plus = 0;
    for (char c : in) {
      if (c == '-') {
        minus++;
      } else {
        plus++;
      }
    }

    std::cout << abs(plus - minus) << "\n";
  }

  return 0;
}
