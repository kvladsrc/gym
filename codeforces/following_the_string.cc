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
    int n;
    std::cin >> n;

    unordered_map<char, int> m;
    for (int i = 0; i <= ('z' - 'a'); ++i) {
      m['a' + i] = 0;
    }

    while (n--) {
      int num;
      std::cin >> num;

      for (auto p : m) {
        if (p.second == num) {
          std::cout << p.first;
          m[p.first]++;
          break;
        }
      }
    }

    std::cout << "\n";
  }

  return 0;
}
