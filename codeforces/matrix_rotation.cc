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

void rotate(vector<int> &m) {
  vector<int> buf(m);
  m[1] = buf[0];
  m[2] = buf[3];
  m[3] = buf[1];
  m[0] = buf[2];
}

bool check(vector<int> &m) {
  if (m[0] < m[1] && m[2] < m[3] && m[0] < m[2] && m[1] < m[3]) return true;
  return false;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    vector<int> m(4);
    for (auto &i : m) {
      std::cin >> i;
    }

    bool res = false;
    for (int i = 0; i < 4; ++i) {
      res = check(m) || res;
      rotate(m);
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
