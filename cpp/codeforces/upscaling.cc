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

    int state = 0;

    for (int i = 1; i <= 2 * n; ++i) {
      string el_f, el_s;
      if (state == 0 || state == 1) {
        el_f = "##";
        el_s = "..";
      } else {
        el_f = "..";
        el_s = "##";
      }

      for (int j = 1; j <= n; ++j) {
        if (j % 2) {
          std::cout << el_f;
        } else {
          std::cout << el_s;
        }
      }

      state = (state + 1) % 4;
      std::cout << "\n";
    }
  }

  return 0;
}
