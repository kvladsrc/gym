#include <iostream>
#include <vector>

using std::cin;
using std::cout;
using std::max;
using std::vector;

static int compute_min_refills(int dist, int tank, vector<int>& stops) {
  int refills = 0;
  int prev = 0;
  int rest = tank;

  stops.push_back(dist);

  for (int const x : stops) {
    if ((x - prev) > tank) {
      return -1;
    }

    if ((x - prev) > rest) {
      rest = tank - (x - prev);
      refills++;
    } else {
      rest = rest - (x - prev);
    }

    prev = x;
  }

  return refills;
}

int main() {
  int d = 0;
  cin >> d;
  int m = 0;
  cin >> m;
  int n = 0;
  cin >> n;

  vector<int> stops(n);
  for (int i = 0; i < n; ++i) {
    cin >> stops.at(i);
  }

  cout << compute_min_refills(d, m, stops) << "\n";

  return 0;
}
