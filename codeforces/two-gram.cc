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
  int n;
  std::cin >> n;

  vector<char> in(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> in[i];
  }

  unordered_map<string, int> m;
  int max = 0;
  string res;
  for (int i = 0; i + 1 < n; ++i) {
    string buf;
    buf += in[i];
    buf += in[i + 1];
    m[buf]++;

    if (m[buf] > max) {
      max = m[buf];
      res = buf;
    }
  }

  std::cout << res << "\n";

  return 0;
}
