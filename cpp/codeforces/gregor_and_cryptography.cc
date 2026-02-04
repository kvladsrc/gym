#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
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
    int p;
    std::cin >> p;

    pair<int, int> res;
    bool done = false;
    for (int i = 3; i < p; ++i) {
      for (int j = 2; j < i; ++j) {
        if (p % i == p % j) {
          done = true;
          res.first = j;
          res.second = i;
          break;
        }
      }

      if (done) break;
    }

    std::cout << res.first << " " << res.second << std::endl;
  }

  return 0;
}
