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
  int n;
  std::cin >> n;
  vector<int> company(n);

  for (int i = 0; i < n; ++i) {
    std::cin >> company[i];
  }

  int max_len = 1;
  for (int i = 0; i < n; ++i) {
    int cur = i;
    int cur_len = 1;
    while (company[cur] != -1) {
      cur = company[cur] - 1;
      cur_len++;
    }

    if (max_len < cur_len) {
      max_len = cur_len;
    }
  }

  std::cout << max_len << "\n";

  return 0;
}
