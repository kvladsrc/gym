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
  string in;
  std::cin >> in;

  vector<int> qs(in.size());
  int num = 0;
  for (size_t i = 0; i < in.size(); ++i) {
    if (in[i] == 'Q') {
      num++;
    }

    qs[i] = num;
  }

  vector<size_t> indices(in.size());
  std::iota(indices.begin(), indices.end(), 0);
  int res = std::accumulate(
      indices.begin(), indices.end(), 0, [&](int sum, size_t i) {
        return sum + (in[i] == 'A' ? qs[i] * (qs.back() - qs[i]) : 0);
      });
  std::cout << res << "\n";

  return 0;
}
